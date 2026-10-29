using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Sharing;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Push;

namespace Yakku.Tests;

public class PushBackoffTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(10, 30)]
    [InlineData(100, 30)]
    public void Ceiling_DoublesEachAttempt_UpToTheCap(int attempt, double expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PushQueueOptions.BackoffCeiling(attempt));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.999)]
    public void Jitter_StaysWithinZeroAndTheCeiling(double random)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var delay = PushQueueOptions.BackoffWithJitter(attempt, random);
            Assert.InRange(delay, TimeSpan.Zero, PushQueueOptions.BackoffCeiling(attempt));
        }
    }
}

public class VoteNotificationPlannerTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TheFirstVotes_AreImmediate_AndStartNoWindow(int sequence)
    {
        var plan = VoteNotificationPlanner.Decide(sequence, null, Now);

        Assert.Equal(VoteNotificationKind.Immediate, plan.Kind);
        Assert.False(plan.OpenNewWindow);
        Assert.False(plan.CloseOpenWindow);
    }

    [Fact]
    public void TheThirdVote_IsImmediate_AndStartsTheFirstWindow()
    {
        var plan = VoteNotificationPlanner.Decide(3, null, Now);

        Assert.Equal(VoteNotificationKind.Immediate, plan.Kind);
        Assert.True(plan.OpenNewWindow);
    }

    [Fact]
    public void ALaterVote_JoinsTheOpenWindow()
    {
        var plan = VoteNotificationPlanner.Decide(4, Now.AddMinutes(10), Now);

        Assert.Equal(VoteNotificationKind.Digest, plan.Kind);
        Assert.False(plan.OpenNewWindow);
        Assert.False(plan.CloseOpenWindow);
    }

    [Fact]
    public void AVoteAtExactlyTheWindowEnd_StartsANewWindow_AndClosesTheOldOne()
    {
        var plan = VoteNotificationPlanner.Decide(9, Now, Now);

        Assert.Equal(VoteNotificationKind.Digest, plan.Kind);
        Assert.True(plan.CloseOpenWindow);
        Assert.True(plan.OpenNewWindow);
    }

    [Fact]
    public void ALaterVoteWithNoWindow_StartsANewWindow()
    {
        // A card backfilled past three votes, or the first vote after a digest was sent.
        var plan = VoteNotificationPlanner.Decide(40, null, Now);

        Assert.Equal(VoteNotificationKind.Digest, plan.Kind);
        Assert.False(plan.CloseOpenWindow);
        Assert.True(plan.OpenNewWindow);
    }
}

public class VoteNotificationClassifyTests
{
    [Fact]
    public void AnAcceptedPush_IsSubmitted()
    {
        var outcome = VoteNotificationDispatcher.Classify(
            new PushUserNotificationResult { EligibleDevices = 2, Successful = 1, Failed = 1, RetryableFailures = 1 },
            null);

        // A partial success is not retried: that would push twice to the device that already got one.
        Assert.Equal(PushOutcome.Submitted, outcome);
    }

    [Fact]
    public void OnlyTransientFailures_AreRetried()
    {
        var outcome = VoteNotificationDispatcher.Classify(
            new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, RetryableFailures = 1 },
            null);

        Assert.Equal(PushOutcome.Retry, outcome);
    }

    [Fact]
    public void AnInvalidToken_IsNotRetried()
    {
        var outcome = VoteNotificationDispatcher.Classify(
            new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, InvalidTokensDeactivated = 1 },
            null);

        Assert.Equal(PushOutcome.Skipped, outcome);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    public void NoEligibleDevice_IsSkipped(int status)
    {
        var outcome = VoteNotificationDispatcher.Classify(null, new AppException(status, "X", "No device."));

        Assert.Equal(PushOutcome.Skipped, outcome);
    }

    [Fact]
    public void AProviderFault_IsRetried()
    {
        var outcome = VoteNotificationDispatcher.Classify(null, new AppException(502, "X", "Failed to send."));

        Assert.Equal(PushOutcome.Retry, outcome);
    }
}

/// <summary>
/// Real-PostgreSQL checks of the outbox (atomic numbering, windows) and the dispatcher (claims, digest filtering).
/// They are skipped when no database is available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VoteNotificationOutboxIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public VoteNotificationOutboxIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task FirstThreeVotes_AreImmediate_AndTheRestJoinOneOpenWindow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);

        for (var i = 0; i < 6; i++)
        {
            await VoteAsync(context, content);
        }

        await using var verify = _fixture.CreateContext();
        var events = await verify.VoteNotificationEvents
            .Where(e => e.ContentId == poll.Id)
            .OrderBy(e => e.Sequence)
            .ToListAsync();
        Assert.Equal([1, 2, 3, 4, 5, 6], events.Select(e => e.Sequence));
        Assert.All(events.Take(3), e => Assert.Equal(VoteNotificationKind.Immediate, e.Kind));
        Assert.All(events.Skip(3), e => Assert.Equal(VoteNotificationKind.Digest, e.Kind));
        var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
        Assert.Equal(VoteDigestWindowStatus.Open, window.Status);
        Assert.All(events.Skip(3), e => Assert.Equal(window.Id, e.WindowId));
        Assert.Equal(6, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == poll.Id)).VoteCount);
        Assert.Equal(6, await verify.Notifications.CountAsync(n => n.UserId == poll.CreatorId));
    }

    [SkippableFact]
    public async Task ConcurrentVotes_AreNumberedWithoutGapsOrRepeats()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(setup);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            await VoteAsync(context, content);
        }));

        await using var verify = _fixture.CreateContext();
        var sequences = await verify.VoteNotificationEvents
            .Where(e => e.ContentId == poll.Id)
            .Select(e => e.Sequence)
            .OrderBy(s => s)
            .ToListAsync();
        Assert.Equal(Enumerable.Range(1, 10), sequences);
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Immediate));
        Assert.Equal(1, await verify.VoteDigestWindows.CountAsync(w => w.ContentId == poll.Id));
    }

    [SkippableFact]
    public async Task ADuplicateVote_LeavesNoCounterIncrementOrEvent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        var voter = VoterIdentity.ForGuest(Guid.NewGuid());
        await VoteAsync(context, content, voter);

        await Assert.ThrowsAsync<AppException>(() => VoteAsync(context, content, voter));

        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.VoteNotificationEvents.CountAsync(e => e.ContentId == poll.Id));
        Assert.Equal(1, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == poll.Id)).VoteCount);
    }

    [SkippableFact]
    public async Task Dispatcher_SendsEachImmediateEventOnce_EvenWhenRunAgain()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        for (var i = 0; i < 5; i++)
        {
            await VoteAsync(context, content);
        }

        var push = new ScriptedPushService(new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });
        var dispatcher = NewDispatcher(context, push, DateTime.UtcNow.AddSeconds(5));

        var first = await dispatcher.ProcessImmediateAsync(CancellationToken.None);
        var second = await dispatcher.ProcessImmediateAsync(CancellationToken.None);

        Assert.Equal(3, first);
        Assert.Equal(0, second);
        Assert.Equal(3, push.Payloads.Count);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Status == VoteNotificationStatus.Submitted));
        Assert.Equal(2, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Status == VoteNotificationStatus.Pending));
    }

    [SkippableFact]
    public async Task Dispatcher_RetriesATransientFailure_AndAnExpiredLeaseIsClaimedAgain()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        await VoteAsync(context, content);

        var push = new ScriptedPushService(
            new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, RetryableFailures = 1 },
            new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });
        var now = DateTime.UtcNow.AddSeconds(5);
        await NewDispatcher(context, push, now).ProcessImmediateAsync(CancellationToken.None);

        await using (var verify = _fixture.CreateContext())
        {
            var pending = await verify.VoteNotificationEvents.SingleAsync(e => e.ContentId == poll.Id);
            Assert.Equal(VoteNotificationStatus.Pending, pending.Status);
            Assert.True(pending.NextAttemptAt > now);
        }

        // A later run (past the backoff) delivers it.
        await NewDispatcher(context, push, now.AddMinutes(1)).ProcessImmediateAsync(CancellationToken.None);

        await using var done = _fixture.CreateContext();
        var sent = await done.VoteNotificationEvents.SingleAsync(e => e.ContentId == poll.Id);
        Assert.Equal(VoteNotificationStatus.Submitted, sent.Status);
        Assert.Equal(2, push.Payloads.Count);
    }

    [SkippableFact]
    public async Task Digest_CountsUnreadEvents_AndLeavesOutThoseReadBeforeTheCutoff()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        for (var i = 0; i < 7; i++)
        {
            await VoteAsync(context, content);
        }

        // The asker reads one of the four digest-bound notifications.
        var readEvent = await context.VoteNotificationEvents
            .Where(e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Digest)
            .OrderBy(e => e.Sequence)
            .FirstAsync();
        var notification = await context.Notifications.SingleAsync(n => n.Id == readEvent.NotificationId);
        notification.MarkAsRead();
        await context.SaveChangesAsync();

        var window = await context.VoteDigestWindows.AsNoTracking().SingleAsync(w => w.ContentId == poll.Id);
        var push = new ScriptedPushService(new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });

        // Before the window ends nothing is sent.
        Assert.Equal(0, await NewDispatcher(context, push, window.EndsAt.AddMinutes(-1)).ProcessWindowsAsync(CancellationToken.None));
        Assert.Empty(push.Payloads);

        await NewDispatcher(context, push, window.EndsAt.AddMinutes(1)).ProcessWindowsAsync(CancellationToken.None);

        var payload = Assert.Single(push.Payloads);
        Assert.Equal("3", payload.Data!["count"]);
        Assert.DoesNotContain("voter", string.Join(' ', payload.Data.Keys), StringComparison.OrdinalIgnoreCase);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Sent, (await verify.VoteDigestWindows.SingleAsync(w => w.Id == window.Id)).Status);
        Assert.Equal(VoteNotificationStatus.Skipped, (await verify.VoteNotificationEvents.SingleAsync(e => e.Id == readEvent.Id)).Status);
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.WindowId == window.Id && e.Status == VoteNotificationStatus.Submitted));

        // Run again: nothing is sent twice.
        await NewDispatcher(context, push, window.EndsAt.AddMinutes(2)).ProcessWindowsAsync(CancellationToken.None);
        Assert.Single(push.Payloads);
    }

    [SkippableFact]
    public async Task Digest_IsSkipped_WhenEveryEventWasReadBeforeTheCutoff()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        for (var i = 0; i < 5; i++)
        {
            await VoteAsync(context, content);
        }

        foreach (var notification in await context.Notifications.Where(n => n.UserId == poll.CreatorId).ToListAsync())
        {
            notification.MarkAsRead();
        }

        await context.SaveChangesAsync();
        var window = await context.VoteDigestWindows.AsNoTracking().SingleAsync(w => w.ContentId == poll.Id);
        var push = new ScriptedPushService(new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });

        await NewDispatcher(context, push, window.EndsAt.AddMinutes(1)).ProcessWindowsAsync(CancellationToken.None);

        Assert.Empty(push.Payloads);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Skipped, (await verify.VoteDigestWindows.SingleAsync(w => w.Id == window.Id)).Status);
    }

    [SkippableFact]
    public async Task AVoteAfterTheWindowEnded_StartsANewWindow_AndTheOldOneIsDue()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (poll, content) = await NewPollAsync(context);
        for (var i = 0; i < 4; i++)
        {
            await VoteAsync(context, content);
        }

        // Age the open window so it has already ended.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vote_digest_windows SET starts_at = starts_at - interval '2 hours', ends_at = ends_at - interval '2 hours' WHERE content_id = {poll.Id}");

        // A fresh context, as the next request would have (the old one still tracks the window it loaded).
        await using var next = _fixture.CreateContext();
        await VoteAsync(next, content);

        await using var verify = _fixture.CreateContext();
        var windows = await verify.VoteDigestWindows.Where(w => w.ContentId == poll.Id).ToListAsync();
        Assert.Equal(2, windows.Count);
        Assert.Single(windows, w => w.Status == VoteDigestWindowStatus.Open);
        Assert.Single(windows, w => w.Status == VoteDigestWindowStatus.Due);
    }

    private VoteNotificationDispatcher NewDispatcher(YakkuDbContext context, ScriptedPushService push, DateTime now)
    {
        return new VoteNotificationDispatcher(
            context,
            push,
            new FixedTimeProvider(now),
            NullLogger<VoteNotificationDispatcher>.Instance);
    }

    /// <summary>One vote through the real repository, recording the outbox in the vote's transaction.</summary>
    private static async Task<VoteReceipt> VoteAsync(YakkuDbContext context, VotableContent content, VoterIdentity? voter = null)
    {
        var outbox = new VoteNotificationOutbox(context, new VoteNotificationSignal());
        return await new VotableContentRepository(context).AddVoteAsync(
            content,
            voter ?? VoterIdentity.ForGuest(Guid.NewGuid()),
            new NewVote(content.Options[0].Id, null, null),
            (receipt, token) => outbox.RecordAsync(content, receipt, token));
    }

    private static async Task<(Poll Poll, VotableContent Content)> NewPollAsync(YakkuDbContext context)
    {
        // The dispatcher works on the whole outbox, so every test starts from an empty one.
        await context.VoteNotificationEvents.ExecuteDeleteAsync();
        await context.VoteDigestWindows.ExecuteDeleteAsync();
        await context.VoteNotificationCards.ExecuteDeleteAsync();

        var sequence = Interlocked.Increment(ref _sequence);
        var owner = new User($"562{sequence:D10}", $"Asker {sequence}", $"outbox-anon-{sequence}");
        context.Users.Add(owner);
        await context.SaveChangesAsync();

        var poll = new Poll(owner.Id, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();

        var content = (await new VotableContentRepository(context).FindByShareCodeAsync(poll.ShareCode))!;
        return (poll, content);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class ScriptedPushService : IPushNotificationService
    {
        private readonly PushUserNotificationResult[] _script;

        public ScriptedPushService(params PushUserNotificationResult[] script) => _script = script;

        public List<NotificationPayload> Payloads { get; } = [];

        public Task<PushUserNotificationResult> SendToUserAsync(
            Guid userId, NotificationPayload payload, CancellationToken cancellationToken = default)
        {
            // The last scripted step repeats once the script runs out.
            var step = _script[Math.Min(Payloads.Count, _script.Length - 1)];
            Payloads.Add(payload);
            return Task.FromResult(step);
        }

        public Task<PushDeviceNotificationResult> SendToDeviceAsync(Device device, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendToDevicesAsync(IReadOnlyList<Device> devices, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendToDeviceAsync(string installationId, string platform, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendTestToDeviceAsync(TestDeviceNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendTestToUserAsync(TestUserNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
