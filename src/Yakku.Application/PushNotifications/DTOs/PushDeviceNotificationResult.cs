namespace Yakku.Application.PushNotifications.DTOs
{
    public class PushDeviceNotificationResult
    {
        public Guid DeviceId { get; init; }
        public string InstallationId { get; init; } = string.Empty;
        public string Platform { get; init; } = string.Empty;
        public bool Success { get; init; }
        public string? FirebaseMessageId { get; init; }
        public bool InvalidTokenDeactivated { get; init; }
    }
}
