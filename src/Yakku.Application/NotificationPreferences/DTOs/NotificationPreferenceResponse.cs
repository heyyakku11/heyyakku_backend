namespace Yakku.Application.NotificationPreferences.DTOs
{
    public class NotificationPreferenceResponse
    {
        public bool PushEnabled { get; set; }
        public bool PollActivityEnabled { get; set; }
        public bool OffersEnabled { get; set; }
    }
}
