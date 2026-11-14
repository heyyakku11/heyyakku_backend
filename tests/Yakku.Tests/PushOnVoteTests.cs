using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Sharing;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Push;
using Yakku.Infrastructure.Redis;

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
/// The whole vote notification pipeline against real PostgreSQL and real Redis: the outbox written with the vote,
/// the classification into individual pushes and batches, the flush, and recovery. Only the push provider is
/// replaced. Skipped when Docker is not available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VoteNotificationPipelineIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;
    private readonly RedisFixture _redis;

    public VoteNotificationPipelineIntegrationTests(PostgresFixture fixture, RedisFixture redis)
    {
        _fixture = fixture;
        _redis = redis;
    }

    // ---------- outbox: written in the vote's transaction, no Redis involved ----------

    [SkippableFact]
    public async Task FirstThreeVotes_AreImmediate_AndLaterOnesWaitUndecided_WithoutTouchingRedis()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);

        await VoteTimesAsync(context, content, 6);

        await using var verify = _fixture.CreateContext();
        var events = await EventsAsync(verify, poll.Id);
        Assert.Equal([1, 2, 3, 4, 5, 6], events.Select(e => e.Sequence));
        Assert.All(events.Take(3), e => Assert.Equal(VoteNotificationKind.Immediate, e.Kind));
        Assert.All(events.Skip(3), e =>
        {
            Assert.Equal(VoteNotificationKind.Undecided, e.Kind);
            Assert.Equal(VoteNotificationStatus.Pending, e.Status);
            Assert.Null(e.WindowId);
        });
        Assert.Empty(await verify.VoteDigestWindows.Where(w => w.ContentId == poll.Id).ToListAsync());
        Assert.Equal(6, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == poll.Id)).VoteCount);
        Assert.Equal(6, await verify.Notifications.CountAsync(n => n.UserId == poll.CreatorId));
        Assert.Equal(0, (long)(await _redis.Database.ExecuteAsync("DBSIZE")));
    }

    [SkippableFact]
    public async Task ConcurrentVotes_AreNumberedWithoutGapsOrRepeats()
    {
        await using var setup = await StartAsync();
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
        Assert.Equal(7, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Undecided));
    }

    [SkippableFact]
    public async Task ADuplicateVote_LeavesNoCounterIncrementOrEvent()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        var voter = VoterIdentity.ForGuest(Guid.NewGuid());
        await VoteAsync(context, content, voter);

        await Assert.ThrowsAsync<AppException>(() => VoteAsync(context, content, voter));

        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.VoteNotificationEvents.CountAsync(e => e.ContentId == poll.Id));
        Assert.Equal(1, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == poll.Id)).VoteCount);
    }

    // ---------- individual pushes ----------

    [SkippableFact]
    public async Task Dispatcher_SendsEachImmediateEventOnce_EvenWhenRunAgain()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 5);
        var p = NewPipeline(context);

        var first = await p.Individual.ProcessImmediateAsync(CancellationToken.None);
        var second = await p.Individual.ProcessImmediateAsync(CancellationToken.None);

        Assert.Equal(3, first);
        Assert.Equal(0, second);
        Assert.Equal(3, p.Push.Payloads.Count);
        Assert.Equal(3, p.Push.Payloads.Select(x => x.Data!["deliveryId"]).Distinct().Count());
        await using var verify = _fixture.CreateContext();
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Status == VoteNotificationStatus.Submitted && e.ProviderMessageId == "fcm-1"));
        // The two later votes are not pushed until they are classified.
        Assert.Equal(2, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Undecided));
    }

    [SkippableFact]
    public async Task ATransientFailure_IsRetriedOnTheScheduleAndKeepsItsDeliveryId_ThenGivenUpAfterFourAttempts()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteAsync(context, content);
        var p = NewPipeline(context, script: [Transient(), Transient(), Transient(), Transient()]);

        await p.Individual.ProcessImmediateAsync(CancellationToken.None);
        await AssertEventAsync(poll.Id, VoteNotificationStatus.Pending, attempts: 1, retryIn: TimeSpan.FromSeconds(5), p.Clock);

        p.Clock.Advance(TimeSpan.FromSeconds(6));
        await p.Individual.ProcessImmediateAsync(CancellationToken.None);
        await AssertEventAsync(poll.Id, VoteNotificationStatus.Pending, attempts: 2, retryIn: TimeSpan.FromSeconds(30), p.Clock);

        p.Clock.Advance(TimeSpan.FromSeconds(31));
        await p.Individual.ProcessImmediateAsync(CancellationToken.None);
        await AssertEventAsync(poll.Id, VoteNotificationStatus.Pending, attempts: 3, retryIn: TimeSpan.FromMinutes(2), p.Clock);

        p.Clock.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));
        await p.Individual.ProcessImmediateAsync(CancellationToken.None);

        await using var verify = _fixture.CreateContext();
        var failed = await verify.VoteNotificationEvents.SingleAsync(e => e.ContentId == poll.Id);
        Assert.Equal(VoteNotificationStatus.Failed, failed.Status);
        Assert.Equal(4, failed.Attempts);
        Assert.Equal(4, p.Push.Payloads.Count);
        Assert.Single(p.Push.Payloads.Select(x => x.Data!["deliveryId"]).Distinct());
    }

    [SkippableFact]
    public async Task AnImmediateEvent_ClaimedByACrashedWorker_IsClaimedAgainWhenItsLeaseRunsOut()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteAsync(context, content);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vote_notification_events SET status = 'SENDING', lease_until = now() - interval '1 minute', lease_token = {Guid.NewGuid()}, attempts = 1 WHERE content_id = {poll.Id}");
        var p = NewPipeline(context);

        await p.Individual.ProcessImmediateAsync(CancellationToken.None);

        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteNotificationStatus.Submitted, (await verify.VoteNotificationEvents.SingleAsync(e => e.ContentId == poll.Id)).Status);
        Assert.Single(p.Push.Payloads);
    }

    // ---------- classification ----------

    [SkippableFact]
    public async Task RapidVotes_AreCollectedIntoOneBatch_AndTheFirstThreeStayImmediate()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 8);
        var p = NewPipeline(context);

        await ClassifyAllAsync(p);

        await using var verify = _fixture.CreateContext();
        var events = await EventsAsync(verify, poll.Id);
        Assert.All(events.Take(3), e => Assert.Equal(VoteNotificationKind.Immediate, e.Kind));
        var batched = events.Skip(3).ToList();
        Assert.All(batched, e =>
        {
            Assert.Equal(VoteNotificationKind.Digest, e.Kind);
            Assert.Equal(VoteNotificationStatus.Pending, e.Status);
        });
        var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
        Assert.Equal(VoteDigestWindowStatus.Open, window.Status);
        Assert.All(batched, e => Assert.Equal(window.Id, e.WindowId));
        Assert.True(window.EndsAt > window.StartsAt);
        Assert.True(window.MaxFlushAt >= window.EndsAt);
        Assert.Equal(VoteDeliveryMode.Batch, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == poll.Id)).DeliveryMode);

        var cardKey = CardKey.For(ShareContentType.Poll, poll.Id);
        Assert.Equal(window.Id.ToString(), (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(cardKey), "BatchId"));
        Assert.NotNull(await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, cardKey));
    }

    [SkippableFact]
    public async Task ASlowCard_KeepsGettingIndividualPushes_UntilItsVotesBunchUp()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 3);
        await AgeEventsAsync(context, poll.Id, TimeSpan.FromMinutes(10));
        await VoteTimesAsync(context, content, 3); // votes 4, 5 and 6, now
        var p = NewPipeline(context);

        await ClassifyAllAsync(p);

        await using var verify = _fixture.CreateContext();
        var events = await EventsAsync(verify, poll.Id);
        Assert.Equal(VoteNotificationKind.Immediate, events[3].Kind); // 1 vote in the last minute
        Assert.Equal(VoteNotificationKind.Immediate, events[4].Kind); // 2 votes
        Assert.Equal(VoteNotificationKind.Digest, events[5].Kind);    // 3 votes: batching starts
        Assert.Null(events[3].WindowId);
        Assert.Equal(1, await verify.VoteDigestWindows.CountAsync(w => w.ContentId == poll.Id));

        // Attempts count push attempts, not classification attempts.
        Assert.All(events.Take(5), e => Assert.Equal(0, e.Attempts));
        await p.Individual.ProcessImmediateAsync(CancellationToken.None);
        Assert.Equal(5, p.Push.Payloads.Count);
    }

    [SkippableFact]
    public async Task EveryCardIsClassifiedFromItsOwnFrequency()
    {
        await using var context = await StartAsync();
        var (busy, busyContent) = await NewPollAsync(context);
        await VoteTimesAsync(context, busyContent, 8);
        var (slow, slowContent) = await AddPollAsync(context);
        await VoteTimesAsync(context, slowContent, 3);
        await AgeEventsAsync(context, slow.Id, TimeSpan.FromMinutes(10));
        await VoteTimesAsync(context, slowContent, 1);
        var p = NewPipeline(context);

        await ClassifyAllAsync(p);

        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.VoteDigestWindows.CountAsync(w => w.ContentId == busy.Id));
        Assert.Equal(0, await verify.VoteDigestWindows.CountAsync(w => w.ContentId == slow.Id));
        Assert.Equal(VoteNotificationKind.Immediate, (await EventsAsync(verify, slow.Id))[3].Kind);
        Assert.Equal(VoteDeliveryMode.Individual, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == slow.Id)).DeliveryMode);
        Assert.Equal(VoteDeliveryMode.Batch, (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == busy.Id)).DeliveryMode);
    }

    [SkippableFact]
    public async Task ABatchedCard_GoesBackToIndividualAfterTwoQuietEvaluations_AndTheDatabaseKnows()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        var cardKey = CardKey.For(ShareContentType.Poll, poll.Id);
        await _redis.Database.KeyDeleteAsync(VoteBatchingRedisStore.VotesKey(cardKey));

        await p.Evaluator.EvaluateAsync(cardKey, CancellationToken.None);
        Assert.Equal(VoteDeliveryMode.Batch, await CardModeAsync(poll.Id));

        await p.Evaluator.EvaluateAsync(cardKey, CancellationToken.None);
        Assert.Equal(VoteDeliveryMode.Individual, await CardModeAsync(poll.Id));
    }

    [SkippableFact]
    public async Task ClassifiersRunningTogether_ClassifyEveryVoteExactlyOnce()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 30);

        var workers = Enumerable.Range(0, 3).Select(_ => _fixture.CreateContext()).ToList();
        var pipelines = workers.Select(w => NewPipeline(w)).ToList();
        try
        {
            // A vote that loses a compare-and-set is retried a few seconds later: move the clock between passes.
            for (var pass = 0; pass < 6; pass++)
            {
                await Task.WhenAll(pipelines.Select(async p =>
                {
                    while (await p.Classifier.ProcessAsync(CancellationToken.None) > 0)
                    {
                    }
                }));
                foreach (var p in pipelines)
                {
                    p.Clock.Advance(TimeSpan.FromSeconds(10));
                }
            }
        }
        finally
        {
            foreach (var worker in workers)
            {
                await worker.DisposeAsync();
            }
        }

        await using var verify = _fixture.CreateContext();
        var events = await EventsAsync(verify, poll.Id);
        Assert.DoesNotContain(events, e => e.Kind == VoteNotificationKind.Undecided);
        Assert.DoesNotContain(events, e => e.Status == VoteNotificationStatus.Sending);
        var windows = await verify.VoteDigestWindows.Where(w => w.ContentId == poll.Id).ToListAsync();
        Assert.Single(windows, w => w.Status == VoteDigestWindowStatus.Open);
        var digest = events.Where(e => e.Kind == VoteNotificationKind.Digest).ToList();
        Assert.All(digest, e => Assert.Contains(windows, w => w.Id == e.WindowId));
        Assert.Equal(27, digest.Count + events.Count(e => e.Kind == VoteNotificationKind.Immediate && e.Sequence > 3));
    }

    // ---------- flushing ----------

    [SkippableFact]
    public async Task ADueBatch_IsSentAsOneAggregatedPush_WithAStableDeliveryId_AndNeverTwice()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 8);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        var cardKey = CardKey.For(ShareContentType.Poll, poll.Id);

        Assert.Equal(0, (await p.Batches.ProcessScheduleAsync(CancellationToken.None)).Handled); // not due yet
        await MakeDueAsync(cardKey);
        await p.Batches.ProcessScheduleAsync(CancellationToken.None);
        await p.Batches.ProcessScheduleAsync(CancellationToken.None);
        p.Clock.Advance(TimeSpan.FromMinutes(2));
        await p.Batches.SweepAsync(CancellationToken.None);

        var payload = Assert.Single(p.Push.Payloads);
        Assert.Equal("5", payload.Data!["count"]);
        Assert.Equal(PushPayloads.BatchTitle, payload.Title);
        Assert.DoesNotContain("voter", string.Join(' ', payload.Data.Keys), StringComparison.OrdinalIgnoreCase);
        await using var verify = _fixture.CreateContext();
        var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
        Assert.Equal(window.Id.ToString(), payload.Data["deliveryId"]);
        Assert.Equal(VoteDigestWindowStatus.Sent, window.Status);
        Assert.Equal(5, window.EventCount);
        Assert.Equal("fcm-1", window.ProviderMessageId);
        Assert.Equal(5, await verify.VoteNotificationEvents.CountAsync(
            e => e.WindowId == window.Id && e.Status == VoteNotificationStatus.Submitted));
        Assert.False(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(cardKey)));
        Assert.Null(await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, cardKey));
    }

    [SkippableFact]
    public async Task ABatch_IsNotMarkedSentUntilTheProviderAccepts_AndIsRetriedOnTheSchedule()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context, script: [Transient(), Accepted()]);
        await ClassifyAllAsync(p);
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));

        await p.Batches.ProcessScheduleAsync(CancellationToken.None);

        await using (var verify = _fixture.CreateContext())
        {
            var failed = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
            Assert.Equal(VoteDigestWindowStatus.Due, failed.Status);
            Assert.Equal(1, failed.Attempts);
            Assert.Equal(0, failed.EventCount);
            Assert.Null(failed.ProviderMessageId);
            Assert.True(failed.NextAttemptAt > p.Clock.GetUtcNow().UtcDateTime);
            Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
                e => e.WindowId == failed.Id && e.Status == VoteNotificationStatus.Pending));
        }

        Assert.Equal(0, await p.Batches.SweepAsync(CancellationToken.None)); // the 5 s backoff has not passed
        p.Clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(1, await p.Batches.SweepAsync(CancellationToken.None));

        Assert.Equal(2, p.Push.Payloads.Count);
        Assert.Equal(p.Push.Payloads[0].Data!["deliveryId"], p.Push.Payloads[1].Data!["deliveryId"]);
        await using var done = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Sent, (await done.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id)).Status);
    }

    [SkippableFact]
    public async Task ABatchThatKeepsFailing_FollowsTheRetrySchedule_ThenFails()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 5);
        var p = NewPipeline(context, script: [Transient()]);
        await ClassifyAllAsync(p);
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));

        await p.Batches.ProcessScheduleAsync(CancellationToken.None);
        var delays = new List<double>();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await using (var verify = _fixture.CreateContext())
            {
                var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
                Assert.Equal(VoteDigestWindowStatus.Due, window.Status);
                delays.Add((window.NextAttemptAt!.Value - p.Clock.GetUtcNow().UtcDateTime).TotalSeconds);
            }

            p.Clock.Advance(TimeSpan.FromMinutes(3));
            await p.Batches.SweepAsync(CancellationToken.None);
        }

        Assert.InRange(delays[0], 3, 5.5);
        Assert.InRange(delays[1], 28, 30.5);
        Assert.InRange(delays[2], 118, 120.5);
        await using var final = _fixture.CreateContext();
        var failed = await final.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
        Assert.Equal(VoteDigestWindowStatus.Failed, failed.Status);
        Assert.Equal(4, failed.Attempts);
        Assert.Equal(4, p.Push.Payloads.Count);
        Assert.Equal(2, await final.VoteNotificationEvents.CountAsync(
            e => e.WindowId == failed.Id && e.Status == VoteNotificationStatus.Failed));
    }

    [SkippableFact]
    public async Task VotesThatArriveWhileABatchIsBeingSent_GoToANewBatch()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = NewPipeline(context, push: new ScriptedPushService(Accepted()) { Gate = gate.Task });
        await ClassifyAllAsync(sending);
        var cardKey = CardKey.For(ShareContentType.Poll, poll.Id);
        await MakeDueAsync(cardKey);

        await using var flushContext = _fixture.CreateContext();
        var flushing = NewPipeline(flushContext, push: sending.Push);
        var flush = Task.Run(() => flushing.Batches.ProcessScheduleAsync(CancellationToken.None));
        await sending.Push.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));

        // The push for the first batch is in flight. More votes arrive and are classified.
        await VoteTimesAsync(context, content, 3);
        await ClassifyAllAsync(sending);
        gate.SetResult();
        await flush;

        await using var verify = _fixture.CreateContext();
        var windows = await verify.VoteDigestWindows.Where(w => w.ContentId == poll.Id).OrderBy(w => w.StartsAt).ToListAsync();
        Assert.Equal(2, windows.Count);
        Assert.Equal(VoteDigestWindowStatus.Sent, windows[0].Status);
        Assert.Equal(VoteDigestWindowStatus.Open, windows[1].Status);
        Assert.Equal(3, windows[0].EventCount);
        Assert.Equal("3", sending.Push.Payloads.Single().Data!["count"]);
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.WindowId == windows[1].Id && e.Status == VoteNotificationStatus.Pending));
        Assert.Equal(windows[1].Id.ToString(), (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(cardKey), "BatchId"));
    }

    [SkippableFact]
    public async Task AVoteForABatchTheDatabaseAlreadyClaimed_ClosesItInRedis_AndStartsANewBatch()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        var cardKey = CardKey.For(ShareContentType.Poll, poll.Id);
        var oldBatch = (await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(cardKey), "BatchId")).ToString();

        // A worker has claimed the batch but has not yet closed it in Redis.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vote_digest_windows SET status = 'PROCESSING', lease_until = now() + interval '1 minute', lease_token = {Guid.NewGuid()} WHERE content_id = {poll.Id}");
        await VoteTimesAsync(context, content, 1);
        await ClassifyAllAsync(p);

        await using var verify = _fixture.CreateContext();
        var newest = (await EventsAsync(verify, poll.Id)).Last();
        Assert.Equal(VoteNotificationKind.Digest, newest.Kind);
        Assert.NotEqual(oldBatch, newest.WindowId.ToString());
        var open = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id && w.Status == VoteDigestWindowStatus.Open);
        Assert.Equal(open.Id, newest.WindowId);
        Assert.Equal(open.Id.ToString(), (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(cardKey), "BatchId"));
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(e => e.WindowId == Guid.Parse(oldBatch)));
    }

    [SkippableFact]
    public async Task ManyWorkersFlushingTheSameBatch_SendItExactlyOnce()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 8);
        var setup = NewPipeline(context);
        await ClassifyAllAsync(setup);
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));
        var push = new ScriptedPushService(Accepted());

        var contexts = Enumerable.Range(0, 6).Select(_ => _fixture.CreateContext()).ToList();
        try
        {
            await Task.WhenAll(contexts.Select((c, i) => Task.Run(async () =>
            {
                var p = NewPipeline(c, push: push);
                p.Clock.Advance(TimeSpan.FromMinutes(5)); // past the overdue grace, so the sweep also wants the batch
                if (i % 2 == 0)
                {
                    await p.Batches.ProcessScheduleAsync(CancellationToken.None);
                }
                else
                {
                    await p.Batches.SweepAsync(CancellationToken.None);
                }
            })));
        }
        finally
        {
            foreach (var c in contexts)
            {
                await c.DisposeAsync();
            }
        }

        Assert.Single(push.Payloads);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Sent, (await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id)).Status);
    }

    [SkippableFact]
    public async Task ABatchClaimedByACrashedWorker_IsSentAgainOnceItsLeaseRunsOut()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vote_digest_windows SET status = 'PROCESSING', lease_until = now() - interval '1 minute', lease_token = {Guid.NewGuid()}, attempts = 1 WHERE content_id = {poll.Id}");

        var handled = await p.Batches.SweepAsync(CancellationToken.None);

        Assert.Equal(1, handled);
        Assert.Single(p.Push.Payloads);
        await using var verify = _fixture.CreateContext();
        var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
        Assert.Equal(VoteDigestWindowStatus.Sent, window.Status);
        Assert.Equal(2, window.Attempts);
    }

    [SkippableFact]
    public async Task ABatchWhoseEventsWereReadBeforeTheCutoff_LeavesThemOutOfTheCount()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 7);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);

        // The asker reads one of the four batched notifications.
        var readEvent = await context.VoteNotificationEvents
            .Where(e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Digest)
            .OrderBy(e => e.Sequence)
            .FirstAsync();
        var notification = await context.Notifications.SingleAsync(n => n.Id == readEvent.NotificationId);
        notification.MarkAsRead();
        await context.SaveChangesAsync();
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));

        await p.Batches.ProcessScheduleAsync(CancellationToken.None);

        var payload = Assert.Single(p.Push.Payloads);
        Assert.Equal("3", payload.Data!["count"]);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteNotificationStatus.Skipped, (await verify.VoteNotificationEvents.SingleAsync(e => e.Id == readEvent.Id)).Status);
        Assert.Equal(3, await verify.VoteNotificationEvents.CountAsync(
            e => e.ContentId == poll.Id && e.Kind == VoteNotificationKind.Digest && e.Status == VoteNotificationStatus.Submitted));
    }

    [SkippableFact]
    public async Task ABatchWhoseEventsWereAllRead_IsSkipped_AndSendsNothing()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);

        foreach (var notification in await context.Notifications.Where(n => n.UserId == poll.CreatorId).ToListAsync())
        {
            notification.MarkAsRead();
        }

        await context.SaveChangesAsync();
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));

        await p.Batches.ProcessScheduleAsync(CancellationToken.None);

        Assert.Empty(p.Push.Payloads);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Skipped, (await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id)).Status);
    }

    [SkippableFact]
    public async Task MarkingACardRead_ClearsOnlyThatCardsNotifications_AndKeepsThemOutOfTheBatch()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);

        // A second card of the same asker, whose notifications must stay unread.
        var (_, otherContent) = await AddPollAsync(context, poll.CreatorId);
        await VoteAsync(context, otherContent);

        var service = new Yakku.Application.Notifications.Services.NotificationService(new NotificationRepository(context));
        var updated = await service.MarkContentAsReadAsync(poll.CreatorId, "POLL", poll.Id);
        var again = await service.MarkContentAsReadAsync(poll.CreatorId, "poll", poll.Id);
        await Assert.ThrowsAsync<AppException>(() => service.MarkContentAsReadAsync(poll.CreatorId, "survey", poll.Id));

        Assert.Equal(6, updated);
        Assert.Equal(0, again);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        await MakeDueAsync(CardKey.For(ShareContentType.Poll, poll.Id));
        await p.Batches.ProcessScheduleAsync(CancellationToken.None);

        Assert.Empty(p.Push.Payloads);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.Notifications.CountAsync(n => n.UserId == poll.CreatorId && !n.IsRead));
        Assert.Equal(VoteDigestWindowStatus.Skipped, (await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id)).Status);
    }

    // ---------- Redis trouble ----------

    [SkippableFact]
    public async Task WhenRedisIsDown_TheVoteIsKept_AndClassifiedOnceRedisIsBack()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 4);
        var down = NewPipeline(context, redis: new UnavailableScriptClient());

        var handled = await down.Classifier.ProcessAsync(CancellationToken.None);

        Assert.Equal(1, handled);
        await using (var verify = _fixture.CreateContext())
        {
            var waiting = (await EventsAsync(verify, poll.Id)).Last();
            Assert.Equal(VoteNotificationKind.Undecided, waiting.Kind);
            Assert.Equal(VoteNotificationStatus.Pending, waiting.Status);
            Assert.Null(waiting.LeaseToken);
            Assert.InRange((waiting.NextAttemptAt!.Value - down.Clock.GetUtcNow().UtcDateTime).TotalSeconds, 3, 5.5);
        }

        // Not claimable again before the retry time; claimable after it.
        Assert.Equal(0, await down.Classifier.ProcessAsync(CancellationToken.None));
        var back = NewPipeline(context);
        back.Clock.Advance(TimeSpan.FromSeconds(6));
        Assert.Equal(1, await back.Classifier.ProcessAsync(CancellationToken.None));

        await using var done = _fixture.CreateContext();
        Assert.Equal(VoteNotificationKind.Digest, (await EventsAsync(done, poll.Id)).Last().Kind);
    }

    [SkippableFact]
    public async Task WhenRedisStaysDown_TheVoteJoinsADatabaseOnlyBatch_ThatTheSweepSends()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 4);
        var p = NewPipeline(context, redis: new UnavailableScriptClient());
        p.Clock.Advance(TimeSpan.FromMinutes(3)); // far past RedisFallbackAfterSeconds

        await p.Classifier.ProcessAsync(CancellationToken.None);

        await using (var verify = _fixture.CreateContext())
        {
            var vote = (await EventsAsync(verify, poll.Id)).Last();
            Assert.Equal(VoteNotificationKind.Digest, vote.Kind);
            var window = await verify.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id);
            Assert.Equal(VoteDigestWindowStatus.Open, window.Status);
            Assert.Equal(window.Id, vote.WindowId);
        }

        await p.Batches.SweepAsync(CancellationToken.None);

        Assert.Equal("1", Assert.Single(p.Push.Payloads).Data!["count"]);
        await using var done = _fixture.CreateContext();
        Assert.Equal(VoteDigestWindowStatus.Sent, (await done.VoteDigestWindows.SingleAsync(w => w.ContentId == poll.Id)).Status);
    }

    [SkippableFact]
    public async Task WhenRedisLosesItsData_TheDatabaseSweepStillSendsTheOpenBatch()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        await _redis.ResetAsync();

        Assert.Equal(0, await p.Batches.SweepAsync(CancellationToken.None)); // not overdue yet
        p.Clock.Advance(TimeSpan.FromMinutes(2));
        await p.Batches.SweepAsync(CancellationToken.None);

        Assert.Equal("3", Assert.Single(p.Push.Payloads).Data!["count"]);
    }

    [SkippableFact]
    public async Task AfterRedisLosesItsData_ANewVoteStartsANewBatch_AndTheForgottenOneIsHandedToTheSender()
    {
        await using var context = await StartAsync();
        var (poll, content) = await NewPollAsync(context);
        await VoteTimesAsync(context, content, 6);
        var p = NewPipeline(context);
        await ClassifyAllAsync(p);
        await _redis.ResetAsync();

        await VoteTimesAsync(context, content, 1);
        await ClassifyAllAsync(p);

        await using var verify = _fixture.CreateContext();
        var windows = await verify.VoteDigestWindows.Where(w => w.ContentId == poll.Id).ToListAsync();
        Assert.Equal(2, windows.Count);
        Assert.Single(windows, w => w.Status == VoteDigestWindowStatus.Open);
        Assert.Single(windows, w => w.Status == VoteDigestWindowStatus.Due);

        // The forgotten batch goes out at once; nothing waits for its old end time.
        Assert.Equal(1, await p.Batches.SweepAsync(CancellationToken.None));
        Assert.Equal("3", Assert.Single(p.Push.Payloads).Data!["count"]);
    }

    [SkippableFact]
    public async Task ARedisBatchTheDatabaseNeverSaw_IsDroppedOnlyAfterTheGracePeriod()
    {
        await using var context = await StartAsync();
        var card = CardKey.For(ShareContentType.Poll, Guid.NewGuid());
        var young = NewPipeline(context);
        var impatient = NewPipeline(context, options: new VoteNotificationBatchingOptions { OrphanedPendingSeconds = 1 });
        for (var i = 0; i < 3; i++)
        {
            await young.Engine.DecideAsync(card, Guid.NewGuid(), DateTime.UtcNow, VoteDeliveryMode.Individual, []);
        }

        await MakeDueAsync(card);
        await young.Batches.ProcessScheduleAsync(CancellationToken.None);
        Assert.True(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(card)));

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        await MakeDueAsync(card);
        await impatient.Batches.ProcessScheduleAsync(CancellationToken.None);

        Assert.False(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(card)));
        Assert.Empty(young.Push.Payloads);
    }

    // ---------- personality cards ----------

    [SkippableFact]
    public async Task PersonalityVotes_NotifyTheOwner_FirstThreeAtOnce_ThenBatched_AndOpenThePersonalityCard()
    {
        await using var context = await StartAsync();
        await context.VoteNotificationEvents.ExecuteDeleteAsync();
        await context.VoteDigestWindows.ExecuteDeleteAsync();
        await context.VoteNotificationCards.ExecuteDeleteAsync();
        var (owner, cardId, shareCode) = await CollectingPersonalityCardAsync(context);

        // Four votes on a card that completes at five: none of them is the completing vote.
        for (var i = 0; i < 4; i++)
        {
            await PersonalityVoteAsync(context, shareCode);
        }

        await using (var verify = _fixture.CreateContext())
        {
            var events = await EventsAsync(verify, cardId);
            Assert.Equal([1, 2, 3, 4], events.Select(e => e.Sequence));
            Assert.All(events, e => Assert.Equal(ShareContentType.Personality, e.ContentType));
            Assert.All(events, e => Assert.Equal(owner.Id, e.OwnerId));
            Assert.All(events.Take(3), e => Assert.Equal(VoteNotificationKind.Immediate, e.Kind));
            Assert.Equal(VoteNotificationKind.Undecided, events[3].Kind);

            var inbox = await verify.Notifications
                .Where(n => n.UserId == owner.Id && n.EventType == NotificationEventType.PollAnswered)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();
            Assert.Equal(4, inbox.Count);
            Assert.Equal("1 of 5 friends", inbox[0].Title);
            Assert.Equal("4 of 5 friends", inbox[3].Title);
            Assert.All(inbox, n =>
            {
                using var data = System.Text.Json.JsonDocument.Parse(n.Data!);
                Assert.Equal("personality", data.RootElement.GetProperty("contentType").GetString());
                Assert.Equal(cardId.ToString(), data.RootElement.GetProperty("contentId").GetString());
            });
        }

        var p = NewPipeline(context);
        Assert.Equal(3, await p.Individual.ProcessImmediateAsync(CancellationToken.None));
        await ClassifyAllAsync(p);
        await MakeDueAsync(CardKey.For(ShareContentType.Personality, cardId));
        await p.Batches.ProcessScheduleAsync(CancellationToken.None);

        // Three immediate pushes and one batch, every one of them opening the personality card.
        Assert.Equal(4, p.Push.Payloads.Count);
        Assert.All(p.Push.Payloads, payload =>
        {
            Assert.Equal("personality", payload.Data!["contentType"]);
            Assert.Equal(cardId.ToString(), payload.Data["contentId"]);
            Assert.Equal($"yakku://results/personality/{cardId}", payload.Data["deepLink"]);
        });
        var batch = Assert.Single(p.Push.Payloads, payload => payload.Data!.ContainsKey("digest"));
        Assert.Equal(PushPayloads.BatchTitle, batch.Title);
        Assert.Equal("1", batch.Data!["count"]);
    }

    [SkippableFact]
    public async Task ThePersonalityCardsCompletingVote_SendsOnlyThePersonalityReadyPush()
    {
        await using var context = await StartAsync();
        await context.VoteNotificationEvents.ExecuteDeleteAsync();
        await context.VoteDigestWindows.ExecuteDeleteAsync();
        await context.VoteNotificationCards.ExecuteDeleteAsync();
        var (owner, cardId, shareCode) = await CollectingPersonalityCardAsync(context);

        for (var i = 0; i < 5; i++)
        {
            await PersonalityVoteAsync(context, shareCode);
        }

        await using var verify = _fixture.CreateContext();
        var events = await EventsAsync(verify, cardId);
        // Four vote events (votes 1-4) plus the one "personality ready" event: the fifth vote adds no vote event.
        Assert.Equal(5, events.Count);
        var inbox = await verify.Notifications.Where(n => n.UserId == owner.Id).ToListAsync();
        Assert.Equal(4, inbox.Count(n => n.EventType == NotificationEventType.PollAnswered));
        var ready = Assert.Single(inbox, n => n.EventType == NotificationEventType.PersonalityReady);
        var readyEvent = Assert.Single(events, e => e.NotificationId == ready.Id);
        Assert.Equal(VoteNotificationKind.Immediate, readyEvent.Kind);
        Assert.Equal(4, events.Count(e => e.NotificationId != ready.Id));
        Assert.Equal(PersonalityCardStatus.Completed, (await verify.PersonalityCards.SingleAsync(c => c.Id == cardId)).Status);
    }

    private static async Task<(User Owner, Guid CardId, string ShareCode)> CollectingPersonalityCardAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var owner = new User($"563{sequence:D10}", $"personality-owner-{sequence}");
        context.Users.Add(owner);
        await context.SaveChangesAsync();
        await PersonalityServiceFor(context).SubmitSelfAssessmentAsync(owner.Id, PersonalityTraits(1, 2, 3));
        var card = await context.PersonalityCards.AsNoTracking().SingleAsync(c => c.UserId == owner.Id);
        return (owner, card.Id, card.ShareCode);
    }

    private static async Task PersonalityVoteAsync(YakkuDbContext context, string shareCode)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var voter = new User($"564{sequence:D10}", $"personality-voter-{sequence}");
        context.Users.Add(voter);
        await context.SaveChangesAsync();
        await PersonalityServiceFor(context).VoteAsync(
            shareCode,
            VoterIdentity.ForUser(voter.Id),
            PersonalityTraits(1, 4, 8));
    }

    private static Yakku.Application.Personality.Services.PersonalityService PersonalityServiceFor(YakkuDbContext context) =>
        new(
            new PersonalityRepository(context),
            new Yakku.Application.Personality.Validators.SubmitTraitsValidator(),
            new CardLifecycleNotifications(context, new VoteNotificationSignal(), NullLogger<CardLifecycleNotifications>.Instance),
            new VoteNotificationOutbox(context, new VoteNotificationSignal(), new VoteNotificationBatchingOptions()));

    private static Yakku.Application.Personality.DTOs.SubmitTraitsRequest PersonalityTraits(params int[] ids) =>
        new() { TraitIds = ids.Select(PersonalityTestIds.T).ToList() };

    // ---------- helpers ----------

    private async Task<YakkuDbContext> StartAsync()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        Skip.IfNot(_redis.IsAvailable, _redis.SkipReason);
        await _redis.ResetAsync();
        return _fixture.CreateContext();
    }

    private sealed record Pipeline(
        VoteEventClassifier Classifier,
        VoteBatchDispatcher Batches,
        VoteNotificationDispatcher Individual,
        CardModeEvaluator Evaluator,
        VoteBatchingEngine Engine,
        ScriptedPushService Push,
        TestClock Clock);

    private Pipeline NewPipeline(
        YakkuDbContext context,
        IRedisScriptClient? redis = null,
        VoteNotificationBatchingOptions? options = null,
        ScriptedPushService? push = null,
        PushUserNotificationResult[]? script = null)
    {
        options ??= new VoteNotificationBatchingOptions();
        push ??= new ScriptedPushService(script is { Length: > 0 } ? script : [Accepted()]);
        var clock = new TestClock();
        var store = new VoteBatchingRedisStore(redis ?? _redis.Client, options);
        var policy = new VoteBatchingPolicy(options);
        var engine = new VoteBatchingEngine(store, policy);
        var metrics = new VoteNotificationMetrics();
        var sender = new VotePushSender(push, NullLogger<VotePushSender>.Instance);
        var evaluator = new CardModeEvaluator(context, engine, metrics, clock, NullLogger<CardModeEvaluator>.Instance);
        return new Pipeline(
            new VoteEventClassifier(context, engine, policy, store, options, metrics, clock, NullLogger<VoteEventClassifier>.Instance),
            new VoteBatchDispatcher(context, store, sender, evaluator, options, metrics, clock, NullLogger<VoteBatchDispatcher>.Instance),
            new VoteNotificationDispatcher(context, sender, options, metrics, clock, NullLogger<VoteNotificationDispatcher>.Instance),
            evaluator,
            engine,
            push,
            clock);
    }

    private static async Task ClassifyAllAsync(Pipeline pipeline)
    {
        while (await pipeline.Classifier.ProcessAsync(CancellationToken.None) > 0)
        {
        }
    }

    private static PushUserNotificationResult Accepted() =>
        new() { EligibleDevices = 1, Successful = 1, FirebaseMessageId = "fcm-1" };

    private static PushUserNotificationResult Transient() =>
        new() { EligibleDevices = 1, Failed = 1, RetryableFailures = 1 };

    private static Task<List<VoteNotificationEvent>> EventsAsync(YakkuDbContext context, Guid contentId) =>
        context.VoteNotificationEvents.AsNoTracking().Where(e => e.ContentId == contentId).OrderBy(e => e.Sequence).ToListAsync();

    private async Task<VoteDeliveryMode> CardModeAsync(Guid contentId)
    {
        await using var verify = _fixture.CreateContext();
        return (await verify.VoteNotificationCards.SingleAsync(c => c.ContentId == contentId)).DeliveryMode;
    }

    private async Task AssertEventAsync(Guid contentId, VoteNotificationStatus status, int attempts, TimeSpan retryIn, TestClock clock)
    {
        await using var verify = _fixture.CreateContext();
        var ev = await verify.VoteNotificationEvents.SingleAsync(e => e.ContentId == contentId);
        Assert.Equal(status, ev.Status);
        Assert.Equal(attempts, ev.Attempts);
        var wait = (ev.NextAttemptAt!.Value - clock.GetUtcNow().UtcDateTime).TotalSeconds;
        Assert.InRange(wait, retryIn.TotalSeconds - 2, retryIn.TotalSeconds + 0.5);
    }

    /// <summary>Makes a card's open Redis batch due now, as if its flush time had passed.</summary>
    private async Task MakeDueAsync(string cardKey)
    {
        await _redis.Database.HashSetAsync(VoteBatchingRedisStore.PendingKey(cardKey), "NextFlushAt", 1);
        await _redis.Database.SortedSetAddAsync(VoteBatchingRedisStore.FlushQueueKey, cardKey, 1);
    }

    /// <summary>Moves the card's stored events into the past, so they no longer count in the rolling window.</summary>
    private static Task AgeEventsAsync(YakkuDbContext context, Guid contentId, TimeSpan age) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE vote_notification_events SET created_at = created_at - {age} WHERE content_id = {contentId}");

    private static async Task VoteTimesAsync(YakkuDbContext context, VotableContent content, int times)
    {
        for (var i = 0; i < times; i++)
        {
            await VoteAsync(context, content);
        }
    }

    /// <summary>One vote through the real repository, recording the outbox in the vote's transaction.</summary>
    private static async Task<VoteReceipt> VoteAsync(YakkuDbContext context, VotableContent content, VoterIdentity? voter = null)
    {
        var outbox = new VoteNotificationOutbox(context, new VoteNotificationSignal(), new VoteNotificationBatchingOptions());
        return await new VotableContentRepository(context).AddVoteAsync(
            content,
            voter ?? VoterIdentity.ForGuest(Guid.NewGuid()),
            new NewVote(content.Options[0].Id, null, null),
            (receipt, token) => outbox.RecordAsync(content, receipt, token));
    }

    private static async Task<(Poll Poll, VotableContent Content)> NewPollAsync(YakkuDbContext context)
    {
        // The dispatchers work on the whole outbox, so every test starts from an empty one.
        await context.VoteNotificationEvents.ExecuteDeleteAsync();
        await context.VoteDigestWindows.ExecuteDeleteAsync();
        await context.VoteNotificationCards.ExecuteDeleteAsync();
        return await AddPollAsync(context);
    }

    private static async Task<(Poll Poll, VotableContent Content)> AddPollAsync(YakkuDbContext context, Guid? ownerId = null)
    {
        if (ownerId is null)
        {
            var sequence = Interlocked.Increment(ref _sequence);
            var owner = new User($"562{sequence:D10}", $"outbox-anon-{sequence}");
            context.Users.Add(owner);
            await context.SaveChangesAsync();
            ownerId = owner.Id;
        }

        var poll = new Poll(ownerId.Value, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();

        var content = (await new VotableContentRepository(context).FindByShareCodeAsync(poll.ShareCode))!;
        return (poll, content);
    }

    /// <summary>Real time plus an offset the test can move, so retry and overdue times can be reached without waiting.</summary>
    private sealed class TestClock : TimeProvider
    {
        private TimeSpan _offset;

        public void Advance(TimeSpan by) => _offset += by;

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + _offset;
    }

    private sealed class ScriptedPushService : IPushNotificationService
    {
        private readonly PushUserNotificationResult[] _script;
        private readonly object _lock = new();

        public ScriptedPushService(params PushUserNotificationResult[] script) => _script = script;

        /// <summary>When set, a send waits for it after being recorded, so a test can act while a push is in flight.</summary>
        public Task? Gate { get; init; }

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<NotificationPayload> Payloads { get; } = [];

        public async Task<PushUserNotificationResult> SendToUserAsync(
            Guid userId, NotificationPayload payload, CancellationToken cancellationToken = default)
        {
            PushUserNotificationResult step;
            lock (_lock)
            {
                // The last scripted step repeats once the script runs out.
                step = _script[Math.Min(Payloads.Count, _script.Length - 1)];
                Payloads.Add(payload);
            }

            Entered.TrySetResult();
            if (Gate is not null)
            {
                await Gate;
            }

            return step;
        }

        public Task<PushDeviceNotificationResult> SendToDeviceAsync(Device device, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendToDevicesAsync(IReadOnlyList<Device> devices, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendToDeviceAsync(string installationId, string platform, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendTestToDeviceAsync(TestDeviceNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendTestToUserAsync(TestUserNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
