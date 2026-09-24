namespace Yakku.Application.PushNotifications.DTOs
{
    public class TestUserNotificationRequest
    {
        public Guid UserId { get; set; }
        public NotificationPayload Notification { get; set; } = new();
    }
}
