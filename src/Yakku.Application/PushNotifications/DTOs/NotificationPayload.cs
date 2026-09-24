namespace Yakku.Application.PushNotifications.DTOs
{
    public class NotificationPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public Dictionary<string, string>? Data { get; set; }
    }
}
