namespace Yakku.Application.PushNotifications.Batching
{
    /// <summary>One step of the quiet-period table: at least <see cref="MinVotesPerMinute"/> votes in the window.</summary>
    public sealed class QuietPeriodTier
    {
        public int MinVotesPerMinute { get; set; }
        public int QuietPeriodSeconds { get; set; }
    }

    /// <summary>
    /// Settings of the frequency-based vote notification batching. Bound from the <c>VoteNotifications</c>
    /// configuration section (environment variables use <c>VoteNotifications__Name</c>) and validated when the
    /// application starts, so a bad value stops the deploy instead of misbehaving at run time.
    /// </summary>
    public sealed class VoteNotificationBatchingOptions
    {
        public const string SectionName = "VoteNotifications";

        private static readonly QuietPeriodTier[] DefaultQuietPeriods =
        [
            new() { MinVotesPerMinute = 3, QuietPeriodSeconds = 15 },
            new() { MinVotesPerMinute = 6, QuietPeriodSeconds = 10 },
            new() { MinVotesPerMinute = 16, QuietPeriodSeconds = 5 }
        ];

        private static readonly int[] DefaultRetryBackoffSeconds = [5, 30, 120];

        /// <summary>The first votes of a card that are each pushed on their own, whatever the frequency.</summary>
        public int ImmediateVoteLimit { get; set; } = 3;

        /// <summary>Length of the rolling window the voting frequency is measured over.</summary>
        public int FrequencyWindowSeconds { get; set; } = 60;

        /// <summary>A card in individual mode starts batching at this many votes in the window.</summary>
        public int BatchThreshold { get; set; } = 3;

        /// <summary>A window with at most this many votes counts as low frequency.</summary>
        public int IndividualThreshold { get; set; } = 2;

        /// <summary>Consecutive low-frequency evaluations needed before a batched card goes back to individual.</summary>
        public int LowFrequencyPeriodsToReset { get; set; } = 2;

        /// <summary>How often a batched card is re-evaluated for the switch back to individual.</summary>
        public int EvaluationPeriodSeconds { get; set; } = 60;

        /// <summary>A batch is sent at the latest this long after its first vote, however long the votes keep coming.</summary>
        public int MaximumBatchWaitSeconds { get; set; } = 30;

        /// <summary>
        /// Quiet period by frequency: the tier with the highest MinVotesPerMinute not above the current frequency
        /// applies, and the first tier applies below it. Null means the built-in 3 / 6 / 16 votes = 15 / 10 / 5 s.
        /// </summary>
        public List<QuietPeriodTier>? QuietPeriods { get; set; }

        public int SchedulerPollIntervalMs { get; set; } = 1000;

        /// <summary>
        /// While no batch is waiting and no vote has arrived, the Redis schedule is polled this rarely instead of
        /// every <see cref="SchedulerPollIntervalMs"/>, which keeps an idle system from spending Redis requests.
        /// Batches that exist are still polled at the fast interval.
        /// </summary>
        public int IdleRedisPollSeconds { get; set; } = 10;

        /// <summary>Most due batches (and most evaluations) taken from the schedule in one poll.</summary>
        public int SchedulerBatchSize { get; set; } = 20;

        /// <summary>Most undecided votes classified in one run.</summary>
        public int ClassificationBatchSize { get; set; } = 20;

        /// <summary>How often the database is checked for work Redis could not hand out (retries, crashed workers, a lost Redis).</summary>
        public int RecoverySweepIntervalSeconds { get; set; } = 5;

        /// <summary>The sweep only takes an open batch this long after its due time, so it never races the Redis schedule.</summary>
        public int OverdueGraceSeconds { get; set; } = 15;

        /// <summary>A vote being classified by a worker that does not finish within this is classified again.</summary>
        public int ClassificationLeaseSeconds { get; set; } = 30;

        /// <summary>A push being sent by a worker that does not finish within this is assumed abandoned.</summary>
        public int SendLeaseSeconds { get; set; } = 120;

        /// <summary>How long a batch handed out from the Redis schedule stays hidden from other workers.</summary>
        public int QueueLeaseSeconds { get; set; } = 30;

        /// <summary>
        /// A vote that cannot be classified because Redis fails for this long is put into a database-only batch, so
        /// an outage delays notifications by a bounded time and loses nothing.
        /// </summary>
        public int RedisFallbackAfterSeconds { get; set; } = 120;

        /// <summary>Wait before a vote whose classification failed is tried again.</summary>
        public int ClassificationRetrySeconds { get; set; } = 5;

        /// <summary>A batch in Redis that never reached the database is dropped after this long.</summary>
        public int OrphanedPendingSeconds { get; set; } = 300;

        /// <summary>Idle per-card Redis state is deleted after this long. The durable mode lives in the database.</summary>
        public int StateTtlSeconds { get; set; } = 86400;

        /// <summary>Send attempts, the first one included, before a push is given up as failed.</summary>
        public int MaxAttempts { get; set; } = 4;

        /// <summary>
        /// Wait before attempt 2, 3, ... The last value is reused when there are more attempts than values. Null
        /// means 5 s, 30 s, 2 min.
        /// </summary>
        public List<int>? RetryBackoffSeconds { get; set; }

        public TimeSpan FrequencyWindow => TimeSpan.FromSeconds(FrequencyWindowSeconds);
        public TimeSpan MaximumBatchWait => TimeSpan.FromSeconds(MaximumBatchWaitSeconds);
        public TimeSpan EvaluationPeriod => TimeSpan.FromSeconds(EvaluationPeriodSeconds);

        public IReadOnlyList<QuietPeriodTier> EffectiveQuietPeriods =>
            (IReadOnlyList<QuietPeriodTier>?)QuietPeriods ?? DefaultQuietPeriods;

        public IReadOnlyList<int> EffectiveRetryBackoffSeconds =>
            (IReadOnlyList<int>?)RetryBackoffSeconds ?? DefaultRetryBackoffSeconds;

        /// <summary>The wait before the next attempt, given the attempt that just failed (1-based).</summary>
        public TimeSpan RetryDelay(int failedAttempt)
        {
            var delays = EffectiveRetryBackoffSeconds;
            if (delays.Count == 0)
            {
                return TimeSpan.Zero;
            }

            var index = Math.Clamp(failedAttempt - 1, 0, delays.Count - 1);
            return TimeSpan.FromSeconds(delays[index]);
        }

        /// <summary>Returns every problem found; empty when the settings are usable.</summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();

            void Min(string name, int value, int min)
            {
                if (value < min)
                {
                    errors.Add($"{SectionName}:{name} must be at least {min} (was {value}).");
                }
            }

            Min(nameof(ImmediateVoteLimit), ImmediateVoteLimit, 0);
            Min(nameof(FrequencyWindowSeconds), FrequencyWindowSeconds, 1);
            Min(nameof(BatchThreshold), BatchThreshold, 1);
            Min(nameof(IndividualThreshold), IndividualThreshold, 0);
            Min(nameof(LowFrequencyPeriodsToReset), LowFrequencyPeriodsToReset, 1);
            Min(nameof(EvaluationPeriodSeconds), EvaluationPeriodSeconds, 1);
            Min(nameof(MaximumBatchWaitSeconds), MaximumBatchWaitSeconds, 1);
            Min(nameof(SchedulerPollIntervalMs), SchedulerPollIntervalMs, 100);
            Min(nameof(IdleRedisPollSeconds), IdleRedisPollSeconds, 1);
            Min(nameof(SchedulerBatchSize), SchedulerBatchSize, 1);
            Min(nameof(ClassificationBatchSize), ClassificationBatchSize, 1);
            Min(nameof(RecoverySweepIntervalSeconds), RecoverySweepIntervalSeconds, 1);
            Min(nameof(OverdueGraceSeconds), OverdueGraceSeconds, 0);
            Min(nameof(ClassificationLeaseSeconds), ClassificationLeaseSeconds, 5);
            Min(nameof(SendLeaseSeconds), SendLeaseSeconds, 5);
            Min(nameof(QueueLeaseSeconds), QueueLeaseSeconds, 5);
            Min(nameof(RedisFallbackAfterSeconds), RedisFallbackAfterSeconds, 1);
            Min(nameof(ClassificationRetrySeconds), ClassificationRetrySeconds, 1);
            Min(nameof(OrphanedPendingSeconds), OrphanedPendingSeconds, 1);
            Min(nameof(StateTtlSeconds), StateTtlSeconds, 60);
            Min(nameof(MaxAttempts), MaxAttempts, 1);

            if (SchedulerBatchSize > 1000 || ClassificationBatchSize > 1000)
            {
                errors.Add($"{SectionName}: batch sizes are capped at 1000.");
            }

            if (IndividualThreshold >= BatchThreshold)
            {
                errors.Add(
                    $"{SectionName}:{nameof(IndividualThreshold)} ({IndividualThreshold}) must be below " +
                    $"{nameof(BatchThreshold)} ({BatchThreshold}), or a card could not leave batch mode.");
            }

            var tiers = EffectiveQuietPeriods;
            if (tiers.Count == 0)
            {
                errors.Add($"{SectionName}:{nameof(QuietPeriods)} needs at least one tier.");
            }

            for (var i = 0; i < tiers.Count; i++)
            {
                var tier = tiers[i];
                if (tier.MinVotesPerMinute < 1)
                {
                    errors.Add($"{SectionName}:{nameof(QuietPeriods)}[{i}].MinVotesPerMinute must be at least 1.");
                }

                if (i > 0 && tier.MinVotesPerMinute <= tiers[i - 1].MinVotesPerMinute)
                {
                    errors.Add($"{SectionName}:{nameof(QuietPeriods)} must be in increasing MinVotesPerMinute order.");
                }

                if (tier.QuietPeriodSeconds < 1 || tier.QuietPeriodSeconds > MaximumBatchWaitSeconds)
                {
                    errors.Add(
                        $"{SectionName}:{nameof(QuietPeriods)}[{i}].QuietPeriodSeconds must be between 1 and " +
                        $"{nameof(MaximumBatchWaitSeconds)} ({MaximumBatchWaitSeconds}).");
                }
            }

            if (EffectiveRetryBackoffSeconds.Any(seconds => seconds < 0))
            {
                errors.Add($"{SectionName}:{nameof(RetryBackoffSeconds)} cannot contain negative values.");
            }

            return errors;
        }
    }
}
