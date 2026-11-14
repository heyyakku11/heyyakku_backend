using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Redis;

namespace Yakku.Tests;

/// <summary>
/// The batching state in a real Redis: the Lua scripts, their atomicity and the compare-and-set that keeps
/// concurrent votes of one card consistent. Skipped when Docker is not available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class VoteBatchingRedisTests
{
    private readonly RedisFixture _redis;

    public VoteBatchingRedisTests(RedisFixture redis)
    {
        _redis = redis;
    }

    private static string NewCard() => CardKey.For(ShareContentType.Poll, Guid.NewGuid());

    private async Task<(VoteBatchingRedisStore Store, VoteBatchingEngine Engine, VoteNotificationBatchingOptions Options)> StartAsync(
        VoteNotificationBatchingOptions? options = null)
    {
        Skip.IfNot(_redis.IsAvailable, _redis.SkipReason);
        await _redis.ResetAsync();
        options ??= new VoteNotificationBatchingOptions();
        var store = new VoteBatchingRedisStore(_redis.Client, options);
        return (store, new VoteBatchingEngine(store, new VoteBatchingPolicy(options)), options);
    }

    private static RegisterVoteRequest Register(string card, Guid? eventId, DateTime? at = null, IReadOnlyList<CompanionVote>? companions = null) =>
        new(card, eventId, UnixMillis.From(at ?? DateTime.UtcNow), VoteDeliveryMode.Individual, companions ?? []);

    private static Task<VoteDecision> DecideAsync(VoteBatchingEngine engine, string card, Guid? id = null, DateTime? at = null) =>
        engine.DecideAsync(card, id ?? Guid.NewGuid(), at ?? DateTime.UtcNow, VoteDeliveryMode.Individual, []);

    [SkippableFact]
    public async Task ARegisteredVote_IsCountedOnce_EvenWhenRegisteredAgain()
    {
        var (store, _, _) = await StartAsync();
        var card = NewCard();
        var vote = Guid.NewGuid();

        var first = await store.RegisterAsync(Register(card, vote));
        var again = await store.RegisterAsync(Register(card, vote));

        Assert.True(first.Added);
        Assert.Equal(1, first.Frequency);
        Assert.False(again.Added);
        Assert.True(again.Duplicate);
        Assert.Equal(1, again.Frequency);
    }

    [SkippableFact]
    public async Task AVoteOlderThanTheWindow_IsReportedStale_AndNotCounted()
    {
        var (store, _, _) = await StartAsync();

        var snapshot = await store.RegisterAsync(Register(NewCard(), Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-2)));

        Assert.True(snapshot.Stale);
        Assert.False(snapshot.Duplicate);
        Assert.Equal(0, snapshot.Frequency);
    }

    [SkippableFact]
    public async Task VotesThatLeaveTheRollingWindow_StopCounting()
    {
        var (store, _, _) = await StartAsync(new VoteNotificationBatchingOptions { FrequencyWindowSeconds = 1 });
        var card = NewCard();
        await store.RegisterAsync(Register(card, Guid.NewGuid()));
        await store.RegisterAsync(Register(card, Guid.NewGuid()));

        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        var later = await store.RegisterAsync(Register(card, Guid.NewGuid()));

        Assert.Equal(1, later.Frequency);
    }

    [SkippableFact]
    public async Task TheVoteLog_AndTheState_ExpireOnTheirOwn()
    {
        var (store, _, _) = await StartAsync();
        var card = NewCard();

        await store.RegisterAsync(Register(card, Guid.NewGuid()));

        var votesTtl = await _redis.Database.KeyTimeToLiveAsync(VoteBatchingRedisStore.VotesKey(card));
        var stateTtl = await _redis.Database.KeyTimeToLiveAsync(VoteBatchingRedisStore.StateKey(card));
        Assert.InRange(votesTtl!.Value.TotalSeconds, 1, 120);
        Assert.InRange(stateTtl!.Value.TotalSeconds, 60, 86_400);
    }

    [SkippableFact]
    public async Task EarlierVotesSentAlong_AreCounted_AndRepeatingThemChangesNothing()
    {
        var (store, _, _) = await StartAsync();
        var card = NewCard();
        var companions = Enumerable.Range(0, 3)
            .Select(_ => new CompanionVote(Guid.NewGuid(), UnixMillis.From(DateTime.UtcNow.AddSeconds(-10))))
            .ToList();

        var first = await store.RegisterAsync(Register(card, Guid.NewGuid(), companions: companions));
        var second = await store.RegisterAsync(Register(card, Guid.NewGuid(), companions: companions));

        Assert.Equal(4, first.Frequency);
        Assert.Equal(5, second.Frequency);
    }

    [SkippableFact]
    public async Task EveryCard_HasItsOwnFrequencyAndMode()
    {
        var (_, engine, _) = await StartAsync();
        var busy = NewCard();
        var quiet = NewCard();

        VoteDecision last = null!;
        for (var i = 0; i < 6; i++)
        {
            last = await DecideAsync(engine, busy);
        }

        var quietVote = await DecideAsync(engine, quiet);

        Assert.Equal(VoteDeliveryMode.Batch, last.Mode);
        Assert.Equal(6, last.Frequency);
        Assert.Equal(VoteDeliveryMode.Individual, quietVote.Mode);
        Assert.Equal(1, quietVote.Frequency);
        Assert.Null(quietVote.Batch);
        Assert.False(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(quiet)));
    }

    [SkippableFact]
    public async Task TheThirdVoteInTheWindow_OpensABatch_AndSchedulesItsFlushAndEvaluation()
    {
        var (_, engine, options) = await StartAsync();
        var card = NewCard();

        var first = await DecideAsync(engine, card);
        var second = await DecideAsync(engine, card);
        var third = await DecideAsync(engine, card);

        Assert.Null(first.Batch);
        Assert.Null(second.Batch);
        var batch = third.Batch!;
        Assert.True(third.ModeChanged);
        Assert.Equal(1, batch.EventCount);

        var pending = await _redis.Database.HashGetAllAsync(VoteBatchingRedisStore.PendingKey(card));
        var fields = pending.ToDictionary(e => (string)e.Name!, e => (string)e.Value!);
        Assert.Equal(batch.BatchId.ToString(), fields["BatchId"]);
        Assert.Equal("1", fields["PendingEventCount"]);
        Assert.Equal(batch.FirstPendingAt.ToString(), fields["FirstPendingAt"]);
        Assert.Equal(batch.MaxFlushAt, batch.FirstPendingAt + (long)options.MaximumBatchWait.TotalMilliseconds);

        var flushScore = await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, card);
        Assert.Equal(batch.NextFlushAt, (long)flushScore!.Value);
        Assert.NotNull(await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.EvaluateQueueKey, card));
        Assert.Equal("BATCH", (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.StateKey(card), "DeliveryMode"));
    }

    [SkippableFact]
    public async Task LaterVotes_JoinTheSameBatch_AndMoveItsFlushTimeButNotItsStartOrCap()
    {
        var (_, engine, _) = await StartAsync();
        var card = NewCard();
        VoteDecision opener = null!;
        for (var i = 0; i < 3; i++)
        {
            opener = await DecideAsync(engine, card);
        }

        var joiner = await DecideAsync(engine, card);
        var third = await DecideAsync(engine, card);

        Assert.Equal(opener.Batch!.BatchId, joiner.Batch!.BatchId);
        Assert.Equal(opener.Batch.BatchId, third.Batch!.BatchId);
        Assert.Equal(3, third.Batch.EventCount);
        Assert.Equal(opener.Batch.FirstPendingAt, third.Batch.FirstPendingAt);
        Assert.Equal(opener.Batch.MaxFlushAt, third.Batch.MaxFlushAt);
        Assert.True(third.Batch.NextFlushAt >= opener.Batch.NextFlushAt);
        Assert.True(third.Batch.NextFlushAt <= third.Batch.MaxFlushAt);
        var score = await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, card);
        Assert.Equal(third.Batch.NextFlushAt, (long)score!.Value);
    }

    [SkippableFact]
    public async Task AVoteRegisteredTwice_IsNotAddedToTheBatchTwice()
    {
        var (_, engine, _) = await StartAsync();
        var card = NewCard();
        for (var i = 0; i < 3; i++)
        {
            await DecideAsync(engine, card);
        }

        var vote = Guid.NewGuid();
        var first = await DecideAsync(engine, card, vote);
        var retry = await DecideAsync(engine, card, vote);

        Assert.Equal(2, first.Batch!.EventCount);
        Assert.True(retry.Duplicate);
        Assert.Equal(2, retry.Batch!.EventCount);
        Assert.Equal("2", (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(card), "PendingEventCount"));
    }

    [SkippableFact]
    public async Task ABatchNeverWaitsPastThirtySecondsFromItsFirstVote()
    {
        var (_, engine, options) = await StartAsync();
        var card = NewCard();
        var firstAt = DateTime.UtcNow.AddSeconds(-25);
        VoteDecision last = null!;
        for (var i = 0; i < 3; i++)
        {
            last = await DecideAsync(engine, card, at: firstAt.AddMilliseconds(i));
        }

        // A vote right now would normally push the flush 15 s out, i.e. to 40 s after the first.
        var latest = await DecideAsync(engine, card);

        var cap = last.Batch!.FirstPendingAt + (long)options.MaximumBatchWait.TotalMilliseconds;
        Assert.Equal(cap, latest.Batch!.NextFlushAt);
        Assert.Equal(cap, latest.Batch.MaxFlushAt);
    }

    [SkippableFact]
    public async Task ACompareAndSet_FromAnOutdatedStateVersion_ChangesNothing()
    {
        var (store, _, _) = await StartAsync();
        var card = NewCard();
        var policy = new VoteBatchingPolicy(new VoteNotificationBatchingOptions());
        var companions = Enumerable.Range(0, 2).Select(_ => new CompanionVote(Guid.NewGuid(), UnixMillis.From(DateTime.UtcNow))).ToList();

        var snapshot = await store.RegisterAsync(Register(card, Guid.NewGuid(), companions: companions));
        var plan = policy.PlanVote(snapshot, card, UnixMillis.From(DateTime.UtcNow), Guid.NewGuid());
        var applied = await store.ApplyAsync(plan.Apply!);
        var replay = await store.ApplyAsync(plan.Apply!);

        Assert.True(applied.Applied);
        Assert.False(replay.Applied);
        Assert.Equal("1", (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(card), "PendingEventCount"));
    }

    [SkippableFact]
    public async Task ACompareAndSet_FromAnOutdatedBatchGeneration_ChangesNothing()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        VoteDecision opener = null!;
        for (var i = 0; i < 3; i++)
        {
            opener = await DecideAsync(engine, card);
        }

        var snapshot = await store.RegisterAsync(Register(card, null));
        var request = new ApplyDecisionRequest(
            card,
            snapshot.StateVersion,
            ExpectedGeneration: snapshot.Pending!.Generation + 5,
            VoteDeliveryMode.Batch,
            0,
            0,
            new BatchWrite(opener.Batch!.BatchId, 0, 1, 2, 1),
            EvaluationSchedule.None,
            0,
            60_000);

        var result = await store.ApplyAsync(request);

        Assert.False(result.Applied);
        Assert.Equal("1", (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(card), "PendingEventCount"));
    }

    [SkippableFact]
    public async Task ClosingABatch_OnlyClosesTheBatchNamed_AndTheNextVoteStartsANewOne()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        VoteDecision opener = null!;
        for (var i = 0; i < 3; i++)
        {
            opener = await DecideAsync(engine, card);
        }

        var wrong = await store.CloseBatchAsync(card, Guid.NewGuid());
        Assert.False(wrong);
        Assert.True(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(card)));

        var closed = await store.CloseBatchAsync(card, opener.Batch!.BatchId);
        Assert.True(closed);
        Assert.False(await _redis.Database.KeyExistsAsync(VoteBatchingRedisStore.PendingKey(card)));
        Assert.Null(await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, card));

        var next = await DecideAsync(engine, card);
        Assert.NotEqual(opener.Batch.BatchId, next.Batch!.BatchId);
        Assert.Equal(1, next.Batch.EventCount);
    }

    [SkippableFact]
    public async Task ADueBatch_IsHandedOutOnce_UntilItsLeaseRunsOut()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        VoteDecision opener = null!;
        for (var i = 0; i < 3; i++)
        {
            opener = await DecideAsync(engine, card);
        }

        await MakeDueAsync(card);
        var lease = TimeSpan.FromSeconds(1);
        var first = await store.PollDueAsync(20, 20, lease, lease);
        var second = await store.PollDueAsync(20, 20, lease, lease);
        await Task.Delay(TimeSpan.FromMilliseconds(1300));
        var third = await store.PollDueAsync(20, 20, lease, lease);

        var handedOut = Assert.Single(first.Batches);
        Assert.Equal(opener.Batch!.BatchId, handedOut.BatchId);
        Assert.Equal(card, handedOut.CardKey);
        Assert.Empty(second.Batches);
        Assert.Single(third.Batches);
    }

    [SkippableFact]
    public async Task ABatchNotYetDue_IsNotHandedOut_EvenIfAnOlderQueueEntryIs()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        for (var i = 0; i < 3; i++)
        {
            await DecideAsync(engine, card);
        }

        // An entry left over from before the batch was extended: due in the queue, not due on the batch itself.
        await _redis.Database.SortedSetAddAsync(VoteBatchingRedisStore.FlushQueueKey, card, 1);
        var work = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.Empty(work.Batches);
        var score = await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.FlushQueueKey, card);
        Assert.True(score > UnixMillis.From(DateTime.UtcNow));
    }

    [SkippableFact]
    public async Task AQueueEntryWithoutABatch_IsDropped()
    {
        var (store, _, _) = await StartAsync();
        var card = NewCard();
        await _redis.Database.SortedSetAddAsync(VoteBatchingRedisStore.FlushQueueKey, card, 1);

        var work = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.Empty(work.Batches);
        Assert.Equal(0, work.QueueSize);
    }

    [SkippableFact]
    public async Task OnlyTwentyDueBatches_AreHandedOutPerPoll()
    {
        var (store, engine, _) = await StartAsync();
        var cards = Enumerable.Range(0, 25).Select(_ => NewCard()).ToList();
        foreach (var card in cards)
        {
            for (var i = 0; i < 3; i++)
            {
                await DecideAsync(engine, card);
            }

            await MakeDueAsync(card);
        }

        var first = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        var second = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.Equal(20, first.Batches.Count);
        Assert.Equal(5, second.Batches.Count);
        Assert.Equal(25, first.Batches.Concat(second.Batches).Select(b => b.CardKey).Distinct().Count());
    }

    [SkippableFact]
    public async Task ABatchedCard_ReturnsToIndividual_AfterTwoConsecutiveLowFrequencyEvaluations()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        for (var i = 0; i < 3; i++)
        {
            await DecideAsync(engine, card);
        }

        // The card goes quiet: its vote log empties.
        await _redis.Database.KeyDeleteAsync(VoteBatchingRedisStore.VotesKey(card));

        var one = await engine.EvaluateAsync(card, VoteDeliveryMode.Batch);
        var two = await engine.EvaluateAsync(card, VoteDeliveryMode.Batch);

        Assert.Equal(VoteDeliveryMode.Batch, one.Mode);
        Assert.False(one.ModeChanged);
        Assert.Equal(VoteDeliveryMode.Individual, two.Mode);
        Assert.True(two.ModeChanged);
        Assert.Null(await _redis.Database.SortedSetScoreAsync(VoteBatchingRedisStore.EvaluateQueueKey, card));
        Assert.Equal("INDIVIDUAL", (string?)await _redis.Database.HashGetAsync(VoteBatchingRedisStore.StateKey(card), "DeliveryMode"));
        _ = store;
    }

    [SkippableFact]
    public async Task ABusyPeriod_BetweenTwoQuietOnes_KeepsTheCardInBatchMode()
    {
        var (_, engine, _) = await StartAsync();
        var card = NewCard();
        for (var i = 0; i < 3; i++)
        {
            await DecideAsync(engine, card);
        }

        await _redis.Database.KeyDeleteAsync(VoteBatchingRedisStore.VotesKey(card));
        var quiet = await engine.EvaluateAsync(card, VoteDeliveryMode.Batch);
        for (var i = 0; i < 4; i++)
        {
            await DecideAsync(engine, card);
        }

        var busy = await engine.EvaluateAsync(card, VoteDeliveryMode.Batch);
        await _redis.Database.KeyDeleteAsync(VoteBatchingRedisStore.VotesKey(card));
        var quietAgain = await engine.EvaluateAsync(card, VoteDeliveryMode.Batch);

        Assert.Equal(VoteDeliveryMode.Batch, quiet.Mode);
        Assert.Equal(VoteDeliveryMode.Batch, busy.Mode);
        Assert.Equal(VoteDeliveryMode.Batch, quietAgain.Mode);
    }

    [SkippableFact]
    public async Task ADueEvaluation_IsHandedOutOnce_AndNotBeforeItsTime()
    {
        var (store, engine, _) = await StartAsync();
        var card = NewCard();
        for (var i = 0; i < 3; i++)
        {
            await DecideAsync(engine, card);
        }

        var early = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        await _redis.Database.SortedSetAddAsync(VoteBatchingRedisStore.EvaluateQueueKey, card, 1);
        var due = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        var again = await store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        Assert.Empty(early.Evaluations);
        Assert.Equal([card], due.Evaluations);
        Assert.Empty(again.Evaluations);
    }

    [SkippableFact]
    public async Task ConcurrentVotesOnOneCard_EndUpInOneBatch_WithAnExactCount()
    {
        var (_, engine, _) = await StartAsync();
        var card = NewCard();

        var retriedLater = 0;
        var decisions = await Task.WhenAll(Enumerable.Range(0, 40).Select(async _ =>
        {
            // A caller that loses the compare-and-set too often is retried later, as the classifier does.
            var id = Guid.NewGuid();
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    return await DecideAsync(engine, card, id);
                }
                catch (VoteBatchingConflictException) when (attempt < 100)
                {
                    Interlocked.Increment(ref retriedLater);
                    await Task.Delay(5);
                }
            }
        }));

        var batched = decisions.Where(d => d.Batch is not null).ToList();
        var individual = decisions.Where(d => d.Batch is null).ToList();
        Assert.Equal(2, individual.Count); // exactly the first two votes of the window
        Assert.Equal(38, batched.Count);
        Assert.Single(batched.Select(d => d.Batch!.BatchId).Distinct());

        // The Redis count is advisory. It is exact unless a vote had to be retried in a separate call: a retry
        // then sees the vote as already counted and leaves the batch alone (the database holds the real count).
        var count = int.Parse((await _redis.Database.HashGetAsync(VoteBatchingRedisStore.PendingKey(card), "PendingEventCount"))!);
        Assert.InRange(count, 38 - retriedLater, 38);
        Assert.Equal(1, await _redis.Database.SortedSetLengthAsync(VoteBatchingRedisStore.FlushQueueKey));
    }

    [SkippableFact]
    public async Task WhenRedisIsDown_TheStoreFailsWithAStoreException()
    {
        var store = new VoteBatchingRedisStore(new UnavailableScriptClient(), new VoteNotificationBatchingOptions());

        await Assert.ThrowsAsync<VoteBatchingStoreException>(() => store.RegisterAsync(Register(NewCard(), Guid.NewGuid())));
        await Assert.ThrowsAsync<VoteBatchingStoreException>(() => store.PollDueAsync(20, 20, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30)));
        await Assert.ThrowsAsync<VoteBatchingStoreException>(() => store.CloseBatchAsync(NewCard(), Guid.NewGuid()));
    }

    private async Task MakeDueAsync(string card)
    {
        await _redis.Database.HashSetAsync(VoteBatchingRedisStore.PendingKey(card), "NextFlushAt", 1);
        await _redis.Database.SortedSetAddAsync(VoteBatchingRedisStore.FlushQueueKey, card, 1);
    }
}
