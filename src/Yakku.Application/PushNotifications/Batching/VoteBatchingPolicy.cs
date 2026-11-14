using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications.Batching
{
    /// <summary>The batch a vote belongs to, with the schedule Redis now holds for it.</summary>
    public sealed record BatchAssignment(
        Guid BatchId,
        long FirstPendingAt,
        long LastVoteAt,
        long NextFlushAt,
        long MaxFlushAt,
        int Generation,
        int EventCount);

    /// <param name="Mode">The card's delivery mode after this vote.</param>
    /// <param name="Batch">Set when the vote is collected into a batch; null when it is sent on its own.</param>
    public sealed record VoteDecision(
        VoteDeliveryMode Mode,
        int Frequency,
        bool Duplicate,
        bool Stale,
        bool ModeChanged,
        BatchAssignment? Batch);

    public sealed record VotePlan(VoteDecision Decision, ApplyDecisionRequest? Apply);

    public sealed record EvaluationPlan(VoteDeliveryMode Mode, int LowFrequencyPeriods, bool ModeChanged, ApplyDecisionRequest Apply);

    /// <summary>
    /// The business rules of the frequency-based batching as pure functions of a card's own numbers: no clock, no
    /// Redis, no database. Redis scripts only keep the numbers atomically; every decision is made here. All times are
    /// Unix milliseconds on the Redis clock.
    /// </summary>
    public sealed class VoteBatchingPolicy
    {
        private readonly VoteNotificationBatchingOptions _options;

        public VoteBatchingPolicy(VoteNotificationBatchingOptions options)
        {
            _options = options;
        }

        /// <summary>Individual becomes Batch at the batch threshold. Batch is never left because of a single vote.</summary>
        public VoteDeliveryMode ModeAfterVote(VoteDeliveryMode current, int frequency)
        {
            return current == VoteDeliveryMode.Individual && frequency >= _options.BatchThreshold
                ? VoteDeliveryMode.Batch
                : current;
        }

        /// <summary>
        /// One scheduled evaluation of a card. A batched card with at most the individual threshold of votes in the
        /// window has a low period; reaching the configured number of consecutive low periods switches it back.
        /// Any busier window starts the count again.
        /// </summary>
        public (VoteDeliveryMode Mode, int LowFrequencyPeriods) EvaluateIdle(
            VoteDeliveryMode mode,
            int lowFrequencyPeriods,
            int frequency)
        {
            if (mode != VoteDeliveryMode.Batch)
            {
                return (mode, 0);
            }

            if (frequency > _options.IndividualThreshold)
            {
                return (VoteDeliveryMode.Batch, 0);
            }

            var low = lowFrequencyPeriods + 1;
            return low >= _options.LowFrequencyPeriodsToReset
                ? (VoteDeliveryMode.Individual, 0)
                : (VoteDeliveryMode.Batch, low);
        }

        /// <summary>
        /// The mode a card starts from when Redis has no state for it: the stored mode, unless the card has been
        /// quiet for as long as the evaluations would have needed to switch it back (Redis lost the evaluation).
        /// </summary>
        public VoteDeliveryMode EffectiveDurableMode(VoteDeliveryMode stored, DateTime? lastVoteAt, DateTime now)
        {
            if (stored != VoteDeliveryMode.Batch)
            {
                return stored;
            }

            var quietFor = _options.EvaluationPeriod * _options.LowFrequencyPeriodsToReset;
            return lastVoteAt is null || now - lastVoteAt.Value >= quietFor
                ? VoteDeliveryMode.Individual
                : VoteDeliveryMode.Batch;
        }

        /// <summary>The quiet period for a frequency: the highest tier not above it, the first tier below all of them.</summary>
        public long QuietPeriodMs(int frequency)
        {
            var tiers = _options.EffectiveQuietPeriods;
            var chosen = tiers[0];
            foreach (var tier in tiers)
            {
                if (tier.MinVotesPerMinute <= frequency)
                {
                    chosen = tier;
                }
            }

            return chosen.QuietPeriodSeconds * 1000L;
        }

        public long MaxFlushAt(long firstPendingAt) => firstPendingAt + (long)_options.MaximumBatchWait.TotalMilliseconds;

        /// <summary>
        /// <c>MIN(LastVoteAt + QuietPeriod, FirstPendingAt + MaxWait)</c>. The cap is measured from the first pending
        /// vote and never moves, so continuous voting cannot postpone the batch indefinitely.
        /// </summary>
        public long NextFlushAt(long firstPendingAt, long lastVoteAt, int frequency)
        {
            return Math.Min(lastVoteAt + QuietPeriodMs(frequency), MaxFlushAt(firstPendingAt));
        }

        /// <summary>Decides what a registered vote becomes, and the atomic write that records the decision.</summary>
        public VotePlan PlanVote(VoteBatchSnapshot snapshot, string cardKey, long eventAt, Guid candidateBatchId)
        {
            var newMode = ModeAfterVote(snapshot.Mode, snapshot.Frequency);
            var modeChanged = newMode != snapshot.Mode;

            if (newMode == VoteDeliveryMode.Individual)
            {
                return new VotePlan(
                    new VoteDecision(newMode, snapshot.Frequency, snapshot.Duplicate, snapshot.Stale, false, null),
                    null);
            }

            // A vote cannot be from the future, and an old one only counts as late, never as the latest.
            var at = Math.Min(eventAt, snapshot.Now);
            var stateTtlMs = _options.StateTtlSeconds * 1000L;
            var nextEvaluation = snapshot.Now + (long)_options.EvaluationPeriod.TotalMilliseconds;
            var lowPeriods = modeChanged ? 0 : snapshot.LowFrequencyPeriods;

            if (snapshot.Pending is { } open)
            {
                if (snapshot.Duplicate && !modeChanged)
                {
                    // A retry of a vote already in this batch: nothing moves.
                    return new VotePlan(
                        Decision(snapshot, modeChanged, ToAssignment(open, open.Generation, open.EventCount)),
                        null);
                }

                var last = Math.Max(open.LastVoteAt, at);
                var next = NextFlushAt(open.FirstPendingAt, last, snapshot.Frequency);
                var delta = snapshot.Duplicate ? 0 : 1;
                var apply = new ApplyDecisionRequest(
                    cardKey,
                    snapshot.StateVersion,
                    open.Generation,
                    newMode,
                    lowPeriods,
                    last,
                    new BatchWrite(open.BatchId, open.FirstPendingAt, next, open.MaxFlushAt, delta),
                    EvaluationSchedule.AddIfAbsent,
                    nextEvaluation,
                    stateTtlMs);
                var assignment = new BatchAssignment(
                    open.BatchId, open.FirstPendingAt, last, next, open.MaxFlushAt, open.Generation + 1, open.EventCount + delta);
                return new VotePlan(Decision(snapshot, modeChanged, assignment), apply);
            }

            var firstPending = at;
            var firstNext = NextFlushAt(firstPending, firstPending, snapshot.Frequency);
            var maxFlush = MaxFlushAt(firstPending);
            var created = new ApplyDecisionRequest(
                cardKey,
                snapshot.StateVersion,
                -1,
                newMode,
                lowPeriods,
                at,
                new BatchWrite(candidateBatchId, firstPending, firstNext, maxFlush, 1),
                EvaluationSchedule.AddIfAbsent,
                nextEvaluation,
                stateTtlMs);
            return new VotePlan(
                Decision(snapshot, modeChanged, new BatchAssignment(candidateBatchId, firstPending, at, firstNext, maxFlush, 1, 1)),
                created);
        }

        /// <summary>Decides one scheduled hysteresis evaluation of a batched card.</summary>
        public EvaluationPlan PlanEvaluation(VoteBatchSnapshot snapshot, string cardKey)
        {
            var (mode, low) = EvaluateIdle(snapshot.Mode, snapshot.LowFrequencyPeriods, snapshot.Frequency);
            var apply = new ApplyDecisionRequest(
                cardKey,
                snapshot.StateVersion,
                snapshot.Pending?.Generation ?? -1,
                mode,
                low,
                0,
                null,
                mode == VoteDeliveryMode.Batch ? EvaluationSchedule.Set : EvaluationSchedule.Remove,
                snapshot.Now + (long)_options.EvaluationPeriod.TotalMilliseconds,
                _options.StateTtlSeconds * 1000L);
            return new EvaluationPlan(mode, low, mode != snapshot.Mode, apply);
        }

        private static VoteDecision Decision(VoteBatchSnapshot snapshot, bool modeChanged, BatchAssignment batch) =>
            new(VoteDeliveryMode.Batch, snapshot.Frequency, snapshot.Duplicate, snapshot.Stale, modeChanged, batch);

        private static BatchAssignment ToAssignment(PendingBatch batch, int generation, int count) =>
            new(batch.BatchId, batch.FirstPendingAt, batch.LastVoteAt, batch.NextFlushAt, batch.MaxFlushAt, generation, count);
    }
}
