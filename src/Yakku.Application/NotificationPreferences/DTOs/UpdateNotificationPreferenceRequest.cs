namespace Yakku.Application.NotificationPreferences.DTOs
{
    public class UpdateNotificationPreferenceRequest
    {
        public bool? PushEnabled { get; set; }
        public bool? PollActivityEnabled { get; set; }
        public bool? OffersEnabled { get; set; }
        public bool? AlertsEnabled { get; set; }
        public bool? NormalEnabled { get; set; }
    }
}
