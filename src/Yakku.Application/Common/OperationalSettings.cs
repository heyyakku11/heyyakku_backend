namespace Yakku.Application.Common
{
    /// <summary>
    /// Fixed operational settings for guests, audit logs and monitoring. Change them here and redeploy;
    /// they are intentionally not read from the environment.
    /// </summary>
    public static class OperationalSettings
    {
        public static class Guests
        {
            /// <summary>Days an expired guest is kept before it is purged.</summary>
            public const int CleanupGraceDays = 7;

            /// <summary>How often the guest cleanup job runs.</summary>
            public const int CleanupIntervalMinutes = 60;

            /// <summary>Guests handled per cleanup run.</summary>
            public const int CleanupBatchSize = 100;

            /// <summary>Delay before the first cleanup run after startup.</summary>
            public const int CleanupInitialDelayMinutes = 1;

            /// <summary>POST /api/v1/guests requests allowed per IP per minute.</summary>
            public const int CreateRateLimitPerMinute = 20;
        }

        public static class AccountDeletion
        {
            /// <summary>Days between a delete request and the permanent purge; logging in again cancels it.</summary>
            public const int GraceDays = 30;

            /// <summary>How often the purge job runs.</summary>
            public const int IntervalMinutes = 60;

            /// <summary>Accounts purged per run.</summary>
            public const int BatchSize = 25;

            /// <summary>Delay before the first purge run after startup.</summary>
            public const int InitialDelayMinutes = 3;
        }

        public static class Sessions
        {
            /// <summary>How often ended sessions are cleaned up and stale device associations released.</summary>
            public const int CleanupIntervalMinutes = 60;

            /// <summary>Delay before the first cleanup run after startup.</summary>
            public const int CleanupInitialDelayMinutes = 2;

            /// <summary>Days a revoked or expired session row is kept before it is deleted.</summary>
            public const int RetentionDays = 30;
        }

        public static class Devices
        {
            /// <summary>Register / update / unregister requests per caller per minute.</summary>
            public const int WriteRateLimitPerMinute = 30;

            /// <summary>List requests per caller per minute.</summary>
            public const int ReadRateLimitPerMinute = 60;
        }

        public static class Retention
        {
            public const int AuthenticationDays = 180;
            public const int NotificationDays = 90;

            /// <summary>Poll history is the longest-lived trail.</summary>
            public const int PollDays = 730;

            public const int SystemLogDays = 30;

            /// <summary>How often the retention job runs.</summary>
            public const int IntervalHours = 24;

            /// <summary>Delay before the first retention run after startup.</summary>
            public const int InitialDelayMinutes = 2;
        }

        public static class Results
        {
            /// <summary>Votes from others a poll or check needs before its results are revealed.</summary>
            public const int MinVotesToReveal = 1;
        }

        public static class Monitoring
        {
            /// <summary>Requests slower than this are written to system_logs.</summary>
            public const int SlowRequestThresholdMs = 1500;
        }
    }
}
