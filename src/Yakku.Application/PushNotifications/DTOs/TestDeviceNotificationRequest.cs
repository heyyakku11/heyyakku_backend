namespace Yakku.Application.PushNotifications.DTOs
{
    public class TestDeviceNotificationRequest
    {
        public string InstallationId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public NotificationPayload Notification { get; set; } = new();
    }
}
