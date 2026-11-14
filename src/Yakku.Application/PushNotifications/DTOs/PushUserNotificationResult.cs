namespace Yakku.Application.PushNotifications.DTOs
{
    public class PushUserNotificationResult
    {
        public Guid UserId { get; init; }
        public int TotalDevices { get; init; }
        public int EligibleDevices { get; init; }
        public int Successful { get; init; }
        public int Failed { get; init; }
        public int InvalidTokensDeactivated { get; init; }

        /// <summary>Failures worth retrying (FCM unavailable, internal error, quota); not invalid tokens.</summary>
        public int RetryableFailures { get; init; }

        /// <summary>FCM message id of the first push the provider accepted; null when none was.</summary>
        public string? FirebaseMessageId { get; init; }
        public bool Success => EligibleDevices > 0 && Failed == 0;
    }
}
