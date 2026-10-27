namespace Yakku.Application.PushNotifications
{
    public static class PushQueueOptions
    {
        public const int Capacity = 100;
        public const int MaxAttempts = 4;
        public static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

        /// <summary>How long a producer waits for a free slot before the job is dropped.</summary>
        public static readonly TimeSpan EnqueueTimeout = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Exponential backoff with full jitter: a random delay in [0, min(MaxDelay, BaseDelay * 2^(attempt - 1))].
        /// <paramref name="attempt"/> is the attempt that just failed (1-based). Spreading retries over the whole
        /// window keeps many failing jobs from retrying in lockstep.
        /// </summary>
        public static TimeSpan BackoffWithJitter(int attempt, double random)
        {
            var ceiling = BackoffCeiling(attempt);
            return TimeSpan.FromMilliseconds(ceiling.TotalMilliseconds * random);
        }

        public static TimeSpan BackoffCeiling(int attempt)
        {
            var exponent = Math.Clamp(attempt - 1, 0, 30);
            var milliseconds = BaseDelay.TotalMilliseconds * Math.Pow(2, exponent);
            return TimeSpan.FromMilliseconds(Math.Min(milliseconds, MaxDelay.TotalMilliseconds));
        }
    }
}
