using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common;
using Yakku.Application.PushNotifications;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Push;

namespace Yakku.Tests;

public class CardShareNudgeIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CardShareNudgeIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ShareNudge_FiresOnce_WhenPollHasZeroFriendVotesAfterOneHour()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var owner = await AddUserAsync(context);
        var poll = await AddPublishedPollAsync(context, owner.Id, "Which firecracker am I?");

        var createdAt = DateTime.UtcNow.AddHours(-(OperationalSettings.CardShareNudge.AfterHours + 0.5));
        await context.Polls
            .Where(p => p.Id == poll.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.CreatedAt, createdAt));
        context.ChangeTracker.Clear();

        var lifecycle = new CardLifecycleNotifications(
            context,
            new VoteNotificationSignal(),
            NullLogger<CardLifecycleNotifications>.Instance);

        await lifecycle.NotifyShareNudgeAsync(DateTime.UtcNow);
        await lifecycle.NotifyShareNudgeAsync(DateTime.UtcNow);

        var rows = await context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == owner.Id && n.EventType == NotificationEventType.NoVotesYet)
            .ToListAsync();

        Assert.Single(rows);
        Assert.Equal(CardActivityCopy.NoVotesYetTitle, rows[0].Title);
        Assert.Contains("Share it again?", rows[0].Body);
    }

    [SkippableFact]
    public async Task ShareNudge_DoesNotFire_WhenAFriendAlreadyVoted()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var poll = await AddPublishedPollAsync(context, owner.Id, "Am I chill?");

        context.PollVotes.Add(PollVote.ForOption(voter.Id, poll.Id, poll.Options.First().Id, reason: null));
        await context.SaveChangesAsync();

        var createdAt = DateTime.UtcNow.AddHours(-(OperationalSettings.CardShareNudge.AfterHours + 0.5));
        await context.Polls
            .Where(p => p.Id == poll.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.CreatedAt, createdAt));
        context.ChangeTracker.Clear();

        var lifecycle = new CardLifecycleNotifications(
            context,
            new VoteNotificationSignal(),
            NullLogger<CardLifecycleNotifications>.Instance);
        await lifecycle.NotifyShareNudgeAsync(DateTime.UtcNow);

        Assert.Equal(0, await context.Notifications.CountAsync(n =>
            n.UserId == owner.Id && n.EventType == NotificationEventType.NoVotesYet));
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var user = new User(
            $"+91{Random.Shared.NextInt64(6000000000, 9999999999)}",
            $"nudge{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Poll> AddPublishedPollAsync(YakkuDbContext context, Guid creatorId, string question)
    {
        var poll = new Poll(creatorId, question, PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        return poll;
    }
}
