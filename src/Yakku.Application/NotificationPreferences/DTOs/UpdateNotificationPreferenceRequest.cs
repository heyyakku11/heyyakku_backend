namespace Yakku.Application.NotificationPreferences.DTOs
{
    public class UpdateNotificationPreferenceRequest
    {
        public bool? PushEnabled { get; set; }
        public bool? PollActivityEnabled { get; set; }
        public bool? OffersEnabled { get; set; }
    }
}
