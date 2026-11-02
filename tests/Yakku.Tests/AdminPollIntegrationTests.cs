using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminPolls.Services;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public class AdminPollIntegrationTests
{
    private static int _userSequence = 7000;
    private readonly PostgresFixture _fixture;

    public AdminPollIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Polls_ListWithCreatorVoteAndReasonCounts_AndFilterByStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var service = NewService(context);
        var creator = await AddUserAsync(context);
        var voter1 = await AddUserAsync(context);
        var voter2 = await AddUserAsync(context);

        var active = await AddPollAsync(context, creator.Id, "Active one?", expiresAt: DateTime.UtcNow.AddDays(1));
        var expired = await AddPollAsync(context, creator.Id, "Expired one?", expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var draft = await AddPollAsync(context, creator.Id, "Draft one?", publish: false);

        context.PollVotes.Add(PollVote.ForOption(voter1.Id, active.Id, active.Options.First().Id, "because", VoterRole.User));
        context.PollVotes.Add(PollVote.ForOption(voter2.Id, active.Id, active.Options.Last().Id, reason: null, VoterRole.User));
        await context.SaveChangesAsync();

        var all = await service.ListAsync(null, 1, 50);
        var row = Assert.Single(all.Items, item => item.Id == active.Id);
        Assert.Equal("Active one?", row.Question);
        Assert.Equal("question", row.PollType);
        Assert.Equal("active", row.Status);
        Assert.Equal(creator.Profile.AnonymousName, row.CreatorAnonymousName);
        Assert.Equal(2, row.VoteCount);
        Assert.Equal(1, row.ReasonCount);
        Assert.NotNull(row.ExpiresAt);

        Assert.Equal("expired", Assert.Single(all.Items, item => item.Id == expired.Id).Status);
        Assert.Equal("draft", Assert.Single(all.Items, item => item.Id == draft.Id).Status);

        var onlyExpired = await service.ListAsync("Expired", 1, 50);
        Assert.Contains(onlyExpired.Items, item => item.Id == expired.Id);
        Assert.DoesNotContain(onlyExpired.Items, item => item.Id == active.Id);
        Assert.All(onlyExpired.Items, item => Assert.Equal("expired", item.Status));

        var onlyActive = await service.ListAsync("active", 1, 50);
        Assert.Contains(onlyActive.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(onlyActive.Items, item => item.Id == expired.Id);
        Assert.DoesNotContain(onlyActive.Items, item => item.Id == draft.Id);
    }

    [SkippableFact]
    public async Task Polls_PageByOffset_TenByDefault_AndRejectAnUnknownStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var service = NewService(context);
        var creator = await AddUserAsync(context);
        for (var i = 0; i < 12; i++)
        {
            await AddPollAsync(context, creator.Id, $"Paging poll {i}?");
        }

        var first = await service.ListAsync(null, 1, 0);
        Assert.Equal(AdminPollService.DefaultPageSize, first.Items.Count);
        Assert.Equal(10, first.Meta.PageSize);
        Assert.True(first.Meta.TotalCount >= 12);
        Assert.True(first.Meta.TotalPages >= 2);

        var second = await service.ListAsync(null, 2, 0);
        Assert.NotEmpty(second.Items);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.True(first.Items[^1].CreatedAt >= second.Items[0].CreatedAt);

        var capped = await service.ListAsync(null, 1, 9999);
        Assert.Equal(AdminPollService.MaxPageSize, capped.Meta.PageSize);

        var bad = await Assert.ThrowsAsync<AppException>(() => service.ListAsync("deleted", 1, 10));
        Assert.Equal(400, bad.StatusCode);
    }

    private static AdminPollService NewService(YakkuDbContext context)
    {
        return new AdminPollService(new AdminPollRepository(context), TimeProvider.System);
    }

    private static async Task<Poll> AddPollAsync(
        YakkuDbContext context, Guid creatorId, string question, DateTime? expiresAt = null, bool publish = true)
    {
        var poll = new Poll(creatorId, question, PollContentType.Question, expiresAt);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        if (publish)
        {
            poll.MarkPublished();
        }

        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        return poll;
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _userSequence);
        var user = new User($"+917{sequence:D9}", $"Admin Poll {sequence}", $"admin-poll-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
