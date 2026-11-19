using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Community.DTOs;
using Yakku.Application.Community.Services;
using Yakku.Application.Community.Validators;
using Yakku.Application.Personality.DTOs;
using Yakku.Application.Personality.Services;
using Yakku.Application.Personality.Validators;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;
using static Yakku.Tests.PersonalityTestIds;

namespace Yakku.Tests;

/// <summary>Queued community questions against real PostgreSQL. Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CommunityQueuedQuestionIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public CommunityQueuedQuestionIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> ShareCodes = new();

    private static string Code(Guid cardId) => ShareCodes[cardId];

    private static CommunityQueuedQuestionService QueuedService(YakkuDbContext context) =>
        new(
            new CommunityQueuedQuestionRepository(context),
            new PersonalityRepository(context),
            new UpsertCommunityQueuedQuestionValidator());

    private static PersonalityService PersonalityService(YakkuDbContext context) =>
        new(new PersonalityRepository(context), new SubmitTraitsValidator());

    private static SubmitTraitsRequest Traits(params int[] ids) => new() { TraitIds = ids.Select(T).ToList() };

    private static UpsertCommunityQueuedQuestionRequest Request(
        string question,
        params string[] tags) =>
        new()
        {
            Question = question,
            Tags = tags.ToList()
        };

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"558{sequence:D10}", $"queued-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Guid> CardIdOfAsync(YakkuDbContext context, Guid userId)
    {
        var card = await context.PersonalityCards.AsNoTracking().SingleAsync(c => c.UserId == userId);
        ShareCodes[card.Id] = card.ShareCode;
        return card.Id;
    }

    private async Task<(User Owner, Guid CardId)> CompletedCardAsync()
    {
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        await PersonalityService(context).SubmitSelfAssessmentAsync(owner.Id, Traits(1, 2, 3));
        var cardId = await CardIdOfAsync(context, owner.Id);

        for (var i = 0; i < 5; i++)
        {
            await using var voterContext = _fixture.CreateContext();
            var voter = await AddUserAsync(voterContext);
            await PersonalityService(voterContext).VoteAsync(
                Code(cardId),
                Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id),
                Traits(1, 4, 8));
        }

        return (owner, cardId);
    }

    [SkippableFact]
    public async Task Create_List_Update_Delete_RoundTrip()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var created = await QueuedService(context).CreateAsync(
            user.Id,
            Request("What do I do about a flaky friend?", "friendships", "family"));

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("What do I do about a flaky friend?", created.Question);
        Assert.Equal(new[] { "friendships", "family" }, created.Tags);
        Assert.Empty(created.PersonalityTraits);

        var listed = await QueuedService(context).ListAsync(user.Id);
        Assert.Single(listed);
        Assert.Equal(created.Id, listed[0].Id);

        var updated = await QueuedService(context).UpdateAsync(
            user.Id,
            created.Id,
            Request("Should I leave this job?", "career", "money"));

        Assert.Equal("Should I leave this job?", updated.Question);
        Assert.Equal(new[] { "career", "money" }, updated.Tags);

        await QueuedService(context).DeleteAsync(user.Id, created.Id);
        Assert.Empty(await QueuedService(context).ListAsync(user.Id));
    }

    [SkippableFact]
    public async Task Create_FourthQuestion_ReturnsConflict()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var service = QueuedService(context);

        for (var i = 0; i < 3; i++)
        {
            await service.CreateAsync(user.Id, Request($"Question {i + 1}?", "love"));
        }

        var error = await Assert.ThrowsAsync<AppException>(
            () => service.CreateAsync(user.Id, Request("One too many?", "love")));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(3, (await service.ListAsync(user.Id)).Count);
    }

    [SkippableFact]
    public async Task Update_And_Delete_ForOtherUser_ReturnNotFound()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var created = await QueuedService(context).CreateAsync(
            owner.Id,
            Request("Owner question?", "career"));

        var updateError = await Assert.ThrowsAsync<AppException>(
            () => QueuedService(context).UpdateAsync(
                other.Id,
                created.Id,
                Request("Hijack?", "money")));
        var deleteError = await Assert.ThrowsAsync<AppException>(
            () => QueuedService(context).DeleteAsync(other.Id, created.Id));

        Assert.Equal(404, updateError.StatusCode);
        Assert.Equal(404, deleteError.StatusCode);
        Assert.Single(await QueuedService(context).ListAsync(owner.Id));
        Assert.Empty(await QueuedService(context).ListAsync(other.Id));
    }

    [SkippableFact]
    public async Task Create_WithInvalidOrEmptyTags_FailsValidation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var service = QueuedService(context);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(user.Id, Request("No tags?", Array.Empty<string>())));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(user.Id, Request("Bad tag?", "not-a-real-tag")));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(user.Id, Request("", "career")));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateAsync(user.Id, Request(new string('x', 281), "career")));

        Assert.Empty(await service.ListAsync(user.Id));
    }

    [SkippableFact]
    public async Task List_IncludesPersonalityTraits_OnlyWhenShareToCommunity()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (owner, _) = await CompletedCardAsync();
        await using var context = _fixture.CreateContext();

        await QueuedService(context).CreateAsync(
            owner.Id,
            Request("Shared traits should show?", "friendships"));

        var withShare = await QueuedService(context).ListAsync(owner.Id);
        Assert.Single(withShare);
        Assert.Equal(3, withShare[0].PersonalityTraits.Count);
        Assert.Equal(
            new[] { T(1), T(4), T(8) }.OrderBy(id => id),
            withShare[0].PersonalityTraits.Select(t => t.Id).OrderBy(id => id));

        await PersonalityService(context).SetCommunitySharingAsync(
            owner.Id,
            new UpdateCommunitySharingRequest { ShareToCommunity = false });

        var withoutShare = await QueuedService(context).ListAsync(owner.Id);
        Assert.Single(withoutShare);
        Assert.Empty(withoutShare[0].PersonalityTraits);
        Assert.Equal(new[] { "friendships" }, withoutShare[0].Tags);
    }

    [SkippableFact]
    public async Task Create_NormalizesTagCasing_AndOrdersByAllowList()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var created = await QueuedService(context).CreateAsync(
            user.Id,
            Request("Tag order?", "Money", "Friendships", "CAREER"));

        Assert.Equal(new[] { "friendships", "career", "money" }, created.Tags);
    }
}
