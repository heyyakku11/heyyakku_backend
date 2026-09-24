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
        public bool Success => EligibleDevices > 0 && Failed == 0;
    }
}
