using Microsoft.EntityFrameworkCore;
using Yakku.Application.Common;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AccountDeletionIntegrationTests
{
    private static int _sequence;
    private static readonly TimeSpan Grace = TimeSpan.FromDays(OperationalSettings.AccountDeletion.GraceDays);
    private readonly PostgresFixture _fixture;

    public AccountDeletionIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Purge_RemovesTheUserAndEverythingTheyOwn_RespectingRestrictForeignKeys()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var doomed = await AddUserAsync(context, deletionRequestedDaysAgo: 31);
        var bystander = await AddUserAsync(context);

        // The doomed user's poll, with a vote from the bystander, an attached upload and media.
        var ownPoll = await AddPollAsync(context, doomed.Id, PollContentType.Image);
        var ownOption = ownPoll.Options.First().Id;
        context.PollVotes.Add(PollVote.ForOption(bystander.Id, ownPoll.Id, ownOption, null));
        var attached = Session(doomed.Id, ownPoll.Id);
        var orphan = Session(doomed.Id, null);
        ownPoll.AttachMedia(new PollMedia(ownPoll.Id, PollMediaType.Image, attached.BucketName, attached.ObjectKey, "image/jpeg", 12, null, "thumbs/own.jpg"));
        context.MediaUploadSessions.AddRange(attached, orphan);

        // The doomed user's vote on somebody else's poll, plus a notification.
        var otherPoll = await AddPollAsync(context, bystander.Id);
        context.PollVotes.Add(PollVote.ForOption(doomed.Id, otherPoll.Id, otherPoll.Options.First().Id, null));
        context.Notifications.Add(new Notification(
            doomed.Id, NotificationType.Normal, NotificationEventType.PollAnswered, "Title", "Body"));
        await context.SaveChangesAsync();

        var result = await new AccountDeletionRepository(context).PurgeAsync(doomed.Id, DateTime.UtcNow);

        Assert.True(result.Deleted);
        Assert.Equal(doomed.PhoneNumber, result.PhoneNumber);
        Assert.Contains(new Yakku.Application.Users.Interfaces.PurgedObject(attached.BucketName, attached.ObjectKey), result.Objects);
        Assert.Contains(new Yakku.Application.Users.Interfaces.PurgedObject(attached.BucketName, orphan.ObjectKey), result.Objects);
        Assert.Contains(new Yakku.Application.Users.Interfaces.PurgedObject(attached.BucketName, "thumbs/own.jpg"), result.Objects);

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Users.AnyAsync(u => u.Id == doomed.Id));
        Assert.False(await verify.UserProfiles.AnyAsync(p => p.UserId == doomed.Id));
        Assert.False(await verify.Polls.AnyAsync(p => p.CreatorId == doomed.Id));
        Assert.False(await verify.PollVotes.AnyAsync(v => v.VoterId == doomed.Id));
        Assert.False(await verify.PollVotes.AnyAsync(v => v.PollId == ownPoll.Id));
        Assert.False(await verify.MediaUploadSessions.AnyAsync(s => s.UserId == doomed.Id));
        Assert.False(await verify.Notifications.AnyAsync(n => n.UserId == doomed.Id));

        Assert.True(await verify.Users.AnyAsync(u => u.Id == bystander.Id));
        Assert.True(await verify.Polls.AnyAsync(p => p.Id == otherPoll.Id));
    }

    [SkippableFact]
    public async Task Purge_DoesNothing_WhenTheGracePeriodHasNotEnded()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context, deletionRequestedDaysAgo: 5);

        var result = await new AccountDeletionRepository(context).PurgeAsync(user.Id, DateTime.UtcNow);

        Assert.False(result.Deleted);
        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Users.AnyAsync(u => u.Id == user.Id));
    }

    [SkippableFact]
    public async Task Purge_DoesNothing_ForAUserWhoLoggedInAgain()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context, deletionRequestedDaysAgo: 31);
        user.CancelDeletion();
        await context.SaveChangesAsync();

        var result = await new AccountDeletionRepository(context).PurgeAsync(user.Id, DateTime.UtcNow);

        Assert.False(result.Deleted);
        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Users.AnyAsync(u => u.Id == user.Id));
    }

    [SkippableFact]
    public async Task ListDue_ReturnsOnlyAccountsPastTheirScheduledDate()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var due = await AddUserAsync(context, deletionRequestedDaysAgo: 40);
        var pending = await AddUserAsync(context, deletionRequestedDaysAgo: 1);
        var active = await AddUserAsync(context);

        var ids = await new AccountDeletionRepository(context).ListDueUserIdsAsync(DateTime.UtcNow, 1000);

        Assert.Contains(due.Id, ids);
        Assert.DoesNotContain(pending.Id, ids);
        Assert.DoesNotContain(active.Id, ids);
    }

    private static MediaUploadSession Session(Guid userId, Guid? pollId)
    {
        return MediaUploadSession.ForUser(
            userId,
            pollId,
            PollMediaType.Image,
            "image-bucket",
            ".jpg",
            "image/jpeg",
            12,
            Interlocked.Increment(ref _sequence),
            DateTime.UtcNow.AddMinutes(-5));
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context, int? deletionRequestedDaysAgo = null)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"558{sequence:D10}", $"User {sequence}", $"deleted-anon-{sequence}");
        if (deletionRequestedDaysAgo is int days)
        {
            user.RequestDeletion(DateTime.UtcNow.AddDays(-days), Grace);
        }

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Poll> AddPollAsync(
        YakkuDbContext context,
        Guid creatorId,
        PollContentType contentType = PollContentType.Question)
    {
        var poll = new Poll(creatorId, "Which presentation is better?", contentType);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        context.Polls.Add(poll);
        await context.SaveChangesAsync();
        return poll;
    }
}
