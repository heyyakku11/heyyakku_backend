using Microsoft.Extensions.Options;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Push;

namespace Yakku.Tests;

public class VoteNotificationPlannerTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheFirstThreeVotes_AreAlwaysImmediate(int sequence)
    {
        Assert.Equal(VoteNotificationKind.Immediate, VoteNotificationPlanner.Decide(sequence, 3));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(40)]
    public void LaterVotes_AreUndecided_UntilTheFrequencyIsKnown(int sequence)
    {
        Assert.Equal(VoteNotificationKind.Undecided, VoteNotificationPlanner.Decide(sequence, 3));
    }

    [Fact]
    public void TheImmediateLimit_IsConfigurable()
    {
        Assert.Equal(VoteNotificationKind.Immediate, VoteNotificationPlanner.Decide(5, 5));
        Assert.Equal(VoteNotificationKind.Undecided, VoteNotificationPlanner.Decide(6, 5));
    }
}

public class VoteNotificationBatchingOptionsTests
{
    [Fact]
    public void TheDefaults_AreValid_AndMatchTheSpecification()
    {
        var options = new VoteNotificationBatchingOptions();

        Assert.Empty(options.Validate());
        Assert.Equal(3, options.ImmediateVoteLimit);
        Assert.Equal(TimeSpan.FromSeconds(60), options.FrequencyWindow);
        Assert.Equal(TimeSpan.FromSeconds(30), options.MaximumBatchWait);
        Assert.Equal(1000, options.SchedulerPollIntervalMs);
        Assert.Equal(20, options.SchedulerBatchSize);
        Assert.Equal(
            [(3, 15), (6, 10), (16, 5)],
            options.EffectiveQuietPeriods.Select(t => (t.MinVotesPerMinute, t.QuietPeriodSeconds)));
    }

    [Fact]
    public void TheRetrySchedule_IsFiveSeconds_ThirtySeconds_TwoMinutes()
    {
        var options = new VoteNotificationBatchingOptions();

        Assert.Equal(TimeSpan.FromSeconds(5), options.RetryDelay(1));
        Assert.Equal(TimeSpan.FromSeconds(30), options.RetryDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(2), options.RetryDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(2), options.RetryDelay(9));
        Assert.Equal(4, options.MaxAttempts);
    }

    [Theory]
    [InlineData(nameof(VoteNotificationBatchingOptions.FrequencyWindowSeconds), 0)]
    [InlineData(nameof(VoteNotificationBatchingOptions.MaximumBatchWaitSeconds), 0)]
    [InlineData(nameof(VoteNotificationBatchingOptions.SchedulerPollIntervalMs), 10)]
    [InlineData(nameof(VoteNotificationBatchingOptions.SchedulerBatchSize), 0)]
    [InlineData(nameof(VoteNotificationBatchingOptions.SchedulerBatchSize), 5000)]
    [InlineData(nameof(VoteNotificationBatchingOptions.MaxAttempts), 0)]
    [InlineData(nameof(VoteNotificationBatchingOptions.LowFrequencyPeriodsToReset), 0)]
    [InlineData(nameof(VoteNotificationBatchingOptions.StateTtlSeconds), 5)]
    public void ABadValue_IsRejected_AndNamed(string property, int value)
    {
        var options = new VoteNotificationBatchingOptions();
        typeof(VoteNotificationBatchingOptions).GetProperty(property)!.SetValue(options, value);

        var errors = options.Validate();

        Assert.NotEmpty(errors);
        Assert.Contains(errors, error => error.Contains(property) || error.Contains("capped"));
    }

    [Fact]
    public void TheIndividualThreshold_MustBeBelowTheBatchThreshold()
    {
        // Equal thresholds would make a batched card unable to leave batch mode.
        var errors = new VoteNotificationBatchingOptions { BatchThreshold = 3, IndividualThreshold = 3 }.Validate();

        Assert.Contains(errors, error => error.Contains(nameof(VoteNotificationBatchingOptions.IndividualThreshold)));
    }

    [Fact]
    public void QuietPeriods_MustIncrease_AndFitTheMaximumWait()
    {
        var unordered = new VoteNotificationBatchingOptions
        {
            QuietPeriods =
            [
                new QuietPeriodTier { MinVotesPerMinute = 6, QuietPeriodSeconds = 10 },
                new QuietPeriodTier { MinVotesPerMinute = 3, QuietPeriodSeconds = 15 }
            ]
        };
        var tooLong = new VoteNotificationBatchingOptions
        {
            MaximumBatchWaitSeconds = 10,
            QuietPeriods = [new QuietPeriodTier { MinVotesPerMinute = 3, QuietPeriodSeconds = 15 }]
        };
        var empty = new VoteNotificationBatchingOptions { QuietPeriods = [] };

        Assert.Contains(unordered.Validate(), error => error.Contains("increasing"));
        Assert.Contains(tooLong.Validate(), error => error.Contains(nameof(VoteNotificationBatchingOptions.MaximumBatchWaitSeconds)));
        Assert.Contains(empty.Validate(), error => error.Contains("at least one tier"));
    }

    [Fact]
    public void TheStartupValidator_FailsWithEveryProblem()
    {
        var validator = new VoteNotificationBatchingOptionsValidator();

        var good = validator.Validate(null, new VoteNotificationBatchingOptions());
        var bad = validator.Validate(null, new VoteNotificationBatchingOptions { MaxAttempts = 0, SchedulerBatchSize = 0 });

        Assert.True(good.Succeeded);
        Assert.True(bad.Failed);
        Assert.Equal(2, bad.Failures!.Count());
    }
}

public class VoteBatchingPolicyTests
{
    private static readonly VoteNotificationBatchingOptions Defaults = new();
    private static readonly VoteBatchingPolicy Policy = new(Defaults);
    private const string Card = "poll:00000000-0000-0000-0000-000000000001";

    [Theory]
    [InlineData(0, VoteDeliveryMode.Individual)]
    [InlineData(1, VoteDeliveryMode.Individual)]
    [InlineData(2, VoteDeliveryMode.Individual)]
    [InlineData(3, VoteDeliveryMode.Batch)]
    [InlineData(50, VoteDeliveryMode.Batch)]
    public void AnIndividualCard_BecomesBatch_AtThreeVotesInTheWindow(int frequency, VoteDeliveryMode expected)
    {
        Assert.Equal(expected, Policy.ModeAfterVote(VoteDeliveryMode.Individual, frequency));
    }

    [Fact]
    public void ABatchedCard_IsNeverLeftBecauseOfASingleVote()
    {
        Assert.Equal(VoteDeliveryMode.Batch, Policy.ModeAfterVote(VoteDeliveryMode.Batch, 0));
        Assert.Equal(VoteDeliveryMode.Batch, Policy.ModeAfterVote(VoteDeliveryMode.Batch, 1));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(2, 15)]
    [InlineData(3, 15)]
    [InlineData(5, 15)]
    [InlineData(6, 10)]
    [InlineData(15, 10)]
    [InlineData(16, 5)]
    [InlineData(500, 5)]
    public void TheQuietPeriod_ShrinksAsVotingGetsFaster(int frequency, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds * 1000L, Policy.QuietPeriodMs(frequency));
    }

    [Theory]
    [InlineData(0, 3, 15_000)]     // first vote: quiet period after it
    [InlineData(10_000, 3, 25_000)] // a later vote pushes the flush out
    [InlineData(20_000, 3, 30_000)] // ... but never past first + 30 s
    [InlineData(28_000, 20, 30_000)]
    [InlineData(5_000, 20, 10_000)]
    public void TheFlushTime_IsTheQuietPeriodAfterTheLastVote_CappedAtThirtySecondsFromTheFirst(
        long lastVoteAt, int frequency, long expected)
    {
        Assert.Equal(expected, Policy.NextFlushAt(firstPendingAt: 0, lastVoteAt, frequency));
        Assert.Equal(30_000, Policy.MaxFlushAt(0));
    }

    [Fact]
    public void Hysteresis_NeedsTwoConsecutiveLowPeriods_AndABusyPeriodStartsOver()
    {
        var mode = VoteDeliveryMode.Batch;
        var low = 0;

        (mode, low) = Policy.EvaluateIdle(mode, low, 2);
        Assert.Equal((VoteDeliveryMode.Batch, 1), (mode, low));

        // A busy period in between resets the count.
        (mode, low) = Policy.EvaluateIdle(mode, low, 5);
        Assert.Equal((VoteDeliveryMode.Batch, 0), (mode, low));

        (mode, low) = Policy.EvaluateIdle(mode, low, 1);
        Assert.Equal((VoteDeliveryMode.Batch, 1), (mode, low));

        (mode, low) = Policy.EvaluateIdle(mode, low, 0);
        Assert.Equal((VoteDeliveryMode.Individual, 0), (mode, low));
    }

    [Fact]
    public void Hysteresis_LeavesAnIndividualCardAlone()
    {
        Assert.Equal((VoteDeliveryMode.Individual, 0), Policy.EvaluateIdle(VoteDeliveryMode.Individual, 0, 100));
    }

    [Fact]
    public void TheNumberOfLowPeriods_IsConfigurable()
    {
        var policy = new VoteBatchingPolicy(new VoteNotificationBatchingOptions { LowFrequencyPeriodsToReset = 3 });

        var (mode, low) = policy.EvaluateIdle(VoteDeliveryMode.Batch, 1, 0);
        Assert.Equal((VoteDeliveryMode.Batch, 2), (mode, low));
        Assert.Equal(VoteDeliveryMode.Individual, policy.EvaluateIdle(VoteDeliveryMode.Batch, 2, 0).Mode);
    }

    [Fact]
    public void AStoredBatchMode_IsTrustedOnlyWhileTheCardIsStillActive()
    {
        var now = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(VoteDeliveryMode.Batch, Policy.EffectiveDurableMode(VoteDeliveryMode.Batch, now.AddSeconds(-10), now));
        Assert.Equal(VoteDeliveryMode.Individual, Policy.EffectiveDurableMode(VoteDeliveryMode.Batch, now.AddMinutes(-3), now));
        Assert.Equal(VoteDeliveryMode.Individual, Policy.EffectiveDurableMode(VoteDeliveryMode.Batch, null, now));
        Assert.Equal(VoteDeliveryMode.Individual, Policy.EffectiveDurableMode(VoteDeliveryMode.Individual, now, now));
    }

    [Fact]
    public void ASlowCardsVote_IsIndividual_AndWritesNothing()
    {
        var plan = Policy.PlanVote(Snapshot(frequency: 2), Card, eventAt: 99_000, Guid.NewGuid());

        Assert.Null(plan.Apply);
        Assert.Null(plan.Decision.Batch);
        Assert.Equal(VoteDeliveryMode.Individual, plan.Decision.Mode);
    }

    [Fact]
    public void TheVoteThatReachesThreeInTheWindow_OpensABatch()
    {
        var batchId = Guid.NewGuid();

        var plan = Policy.PlanVote(Snapshot(frequency: 3), Card, eventAt: 99_000, batchId);

        Assert.True(plan.Decision.ModeChanged);
        Assert.Equal(VoteDeliveryMode.Batch, plan.Decision.Mode);
        var apply = plan.Apply!;
        Assert.Equal(-1, apply.ExpectedGeneration);
        Assert.Equal(batchId, apply.Batch!.BatchId);
        Assert.Equal(99_000, apply.Batch.FirstPendingAt);
        Assert.Equal(99_000 + 15_000, apply.Batch.NextFlushAt);
        Assert.Equal(99_000 + 30_000, apply.Batch.MaxFlushAt);
        Assert.Equal(1, apply.Batch.CountDelta);
        Assert.Equal(EvaluationSchedule.AddIfAbsent, apply.Evaluation);
    }

    [Fact]
    public void AVoteInAnOpenBatch_ExtendsIt_WithTheTierOfThatCardsOwnFrequency()
    {
        var open = new PendingBatch(Guid.NewGuid(), EventCount: 4, FirstPendingAt: 90_000, LastVoteAt: 95_000, NextFlushAt: 110_000, MaxFlushAt: 120_000, Generation: 2);

        var plan = Policy.PlanVote(Snapshot(frequency: 8, mode: VoteDeliveryMode.Batch, pending: open), Card, eventAt: 100_000, Guid.NewGuid());

        var apply = plan.Apply!;
        Assert.Equal(2, apply.ExpectedGeneration);
        Assert.Equal(open.BatchId, apply.Batch!.BatchId);
        Assert.Equal(100_000 + 10_000, apply.Batch.NextFlushAt); // 8 votes/min: 10 s quiet period
        Assert.Equal(120_000, apply.Batch.MaxFlushAt);
        Assert.Equal(1, apply.Batch.CountDelta);
        Assert.Equal(3, plan.Decision.Batch!.Generation);
        Assert.Equal(5, plan.Decision.Batch.EventCount);
        Assert.False(plan.Decision.ModeChanged);
    }

    [Fact]
    public void ContinuousVoting_CannotPostponeABatchPastThirtySecondsFromItsFirstVote()
    {
        var open = new PendingBatch(Guid.NewGuid(), 10, FirstPendingAt: 70_000, LastVoteAt: 98_000, NextFlushAt: 113_000, MaxFlushAt: 100_000, Generation: 6);

        var plan = Policy.PlanVote(Snapshot(frequency: 3, mode: VoteDeliveryMode.Batch, pending: open), Card, eventAt: 99_000, Guid.NewGuid());

        Assert.Equal(100_000, plan.Apply!.Batch!.NextFlushAt);
    }

    [Fact]
    public void ARetriedVote_AlreadyInTheBatch_ChangesNothing()
    {
        var open = new PendingBatch(Guid.NewGuid(), 4, 90_000, 95_000, 110_000, 120_000, 2);

        var plan = Policy.PlanVote(
            Snapshot(frequency: 5, mode: VoteDeliveryMode.Batch, pending: open, added: false),
            Card,
            eventAt: 95_000,
            Guid.NewGuid());

        Assert.Null(plan.Apply);
        Assert.True(plan.Decision.Duplicate);
        Assert.Equal(open.BatchId, plan.Decision.Batch!.BatchId);
        Assert.Equal(4, plan.Decision.Batch.EventCount);
    }

    [Fact]
    public void ALateVote_NeverMovesTheBatchBackwards_OrIntoTheFuture()
    {
        var open = new PendingBatch(Guid.NewGuid(), 4, 90_000, 98_000, 113_000, 120_000, 2);

        // The vote is older than the batch's last vote, and another claims to be from the future.
        var older = Policy.PlanVote(Snapshot(frequency: 5, mode: VoteDeliveryMode.Batch, pending: open), Card, eventAt: 91_000, Guid.NewGuid());
        var future = Policy.PlanVote(Snapshot(frequency: 5, mode: VoteDeliveryMode.Batch, pending: open), Card, eventAt: 500_000, Guid.NewGuid());

        Assert.Equal(98_000, older.Apply!.LastVoteAt);
        Assert.Equal(100_000, future.Apply!.LastVoteAt); // clamped to the Redis clock
    }

    [Fact]
    public void AnEvaluation_ClearsTheScheduleOnceTheCardIsIndividualAgain()
    {
        var stillBatch = Policy.PlanEvaluation(Snapshot(frequency: 1, mode: VoteDeliveryMode.Batch, low: 0), Card);
        var back = Policy.PlanEvaluation(Snapshot(frequency: 1, mode: VoteDeliveryMode.Batch, low: 1), Card);

        Assert.Equal(VoteDeliveryMode.Batch, stillBatch.Mode);
        Assert.Equal(EvaluationSchedule.Set, stillBatch.Apply.Evaluation);
        Assert.Equal(VoteDeliveryMode.Individual, back.Mode);
        Assert.True(back.ModeChanged);
        Assert.Equal(EvaluationSchedule.Remove, back.Apply.Evaluation);
    }

    private static VoteBatchSnapshot Snapshot(
        int frequency,
        VoteDeliveryMode mode = VoteDeliveryMode.Individual,
        PendingBatch? pending = null,
        bool added = true,
        int low = 0)
    {
        return new VoteBatchSnapshot(
            Now: 100_000,
            Frequency: frequency,
            Added: added,
            Stale: false,
            Mode: mode,
            StateVersion: 7,
            LowFrequencyPeriods: low,
            Pending: pending);
    }
}

public class VoteBatchingEngineRetryTests
{
    [Fact]
    public async Task ALostRace_IsRegisteredAgain_AndTheDecisionUsesTheWinningGeneration()
    {
        var store = new ScriptedStore { LoseApplies = 2 };
        var engine = new VoteBatchingEngine(store, new VoteBatchingPolicy(new VoteNotificationBatchingOptions()));

        var decision = await engine.DecideAsync("poll:a", Guid.NewGuid(), DateTime.UtcNow, VoteDeliveryMode.Individual, []);

        Assert.Equal(3, store.Registers);
        Assert.Equal(3, store.Applies);
        Assert.Equal(7, decision.Batch!.Generation);
        Assert.Equal(9, decision.Batch.EventCount);
    }

    [Fact]
    public async Task AfterALostRace_TheVoteIsStillAddedToTheOpenBatch()
    {
        // The first registration counts the vote; the retry sees it again, but it has not reached the batch yet.
        var store = new ScriptedStore { LoseApplies = 1, OpenBatchAtRetry = true };
        var engine = new VoteBatchingEngine(store, new VoteBatchingPolicy(new VoteNotificationBatchingOptions()));

        var decision = await engine.DecideAsync("poll:a", Guid.NewGuid(), DateTime.UtcNow, VoteDeliveryMode.Individual, []);

        Assert.Equal(2, store.Applies);
        Assert.Equal(1, store.LastApply!.Batch!.CountDelta);
        Assert.False(decision.Duplicate);
    }

    [Fact]
    public async Task AStateThatKeepsChanging_EndsInAConflict_NotAnEndlessLoop()
    {
        var store = new ScriptedStore { LoseApplies = int.MaxValue };
        var engine = new VoteBatchingEngine(store, new VoteBatchingPolicy(new VoteNotificationBatchingOptions()));

        await Assert.ThrowsAsync<VoteBatchingConflictException>(
            () => engine.DecideAsync("poll:a", Guid.NewGuid(), DateTime.UtcNow, VoteDeliveryMode.Individual, []));

        Assert.Equal(5, store.Applies);
    }

    private sealed class ScriptedStore : IVoteBatchingStore
    {
        public int LoseApplies { get; set; }

        /// <summary>From the second registration on: the vote is known (not added again) and a batch is open.</summary>
        public bool OpenBatchAtRetry { get; set; }

        public int Registers { get; private set; }
        public int Applies { get; private set; }
        public ApplyDecisionRequest? LastApply { get; private set; }

        public Task<VoteBatchSnapshot> RegisterAsync(RegisterVoteRequest request, CancellationToken cancellationToken = default)
        {
            Registers++;
            var now = UnixMillis.From(DateTime.UtcNow);
            var retry = OpenBatchAtRetry && Registers > 1;
            PendingBatch? pending = retry
                ? new PendingBatch(Guid.NewGuid(), 3, now - 1000, now - 500, now + 10_000, now + 29_000, 4)
                : null;
            return Task.FromResult(new VoteBatchSnapshot(
                Now: now, Frequency: 6, Added: !retry, Stale: false,
                Mode: VoteDeliveryMode.Batch, StateVersion: 1, LowFrequencyPeriods: 0, Pending: pending));
        }

        public Task<ApplyDecisionResult> ApplyAsync(ApplyDecisionRequest request, CancellationToken cancellationToken = default)
        {
            Applies++;
            LastApply = request;
            return Task.FromResult(Applies <= LoseApplies
                ? new ApplyDecisionResult(false, 0, 0)
                : new ApplyDecisionResult(true, 7, 9));
        }

        public Task<bool> CloseBatchAsync(string cardKey, Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<DueWork> PollDueAsync(int batchLimit, int evaluationLimit, TimeSpan batchLease, TimeSpan evaluationLease, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DueWork(0, [], [], 0, 0));
    }
}

public class PushPayloadTests
{
    [Fact]
    public void ABatchPush_HasTheAgreedTextAndCarriesNoVoterData()
    {
        var card = Guid.NewGuid();
        var batch = Guid.NewGuid();

        var payload = PushPayloads.ForBatch("poll", card, 7, batch);

        Assert.Equal("Your card is getting attention!", payload.Title);
        Assert.Equal("7 people have voted on your card. Tap to see the results.", payload.Body);
        Assert.Equal("7", payload.Data!["count"]);
        Assert.Equal(card.ToString(), payload.Data["cardId"]);
        Assert.Equal(card.ToString(), payload.Data["contentId"]);
        Assert.Equal(batch.ToString(), payload.Data["batchId"]);
        Assert.Equal(batch.ToString(), payload.Data["deliveryId"]);
        Assert.Equal($"yakku://results/poll/{card}", payload.Data["deepLink"]);
        Assert.DoesNotContain(payload.Data.Keys, key => key.Contains("voter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ABatchOfOne_ReadsInTheSingular()
    {
        var payload = PushPayloads.ForBatch("check", Guid.NewGuid(), 1, Guid.NewGuid());

        Assert.Equal("1 person has voted on your card. Tap to see the results.", payload.Body);
    }

    [Fact]
    public void TheDeliveryId_IsTheSameOnEveryBuildOfTheSameBatch()
    {
        var card = Guid.NewGuid();
        var batch = Guid.NewGuid();

        Assert.Equal(
            PushPayloads.ForBatch("poll", card, 3, batch).Data!["deliveryId"],
            PushPayloads.ForBatch("poll", card, 3, batch).Data!["deliveryId"]);
    }
}
