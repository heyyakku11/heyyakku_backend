namespace Yakku.Domain.Entities
{
    public class NotificationPreference
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public bool PushEnabled { get; private set; }
        public bool PollActivityEnabled { get; private set; }
        public bool OffersEnabled { get; private set; }
        public bool AlertsEnabled { get; private set; }
        public bool NormalEnabled { get; private set; }
        public bool WhatsAppEnabled { get; private set; }
        public bool SmsEnabled { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private NotificationPreference()
        {
        }

        public NotificationPreference(Guid userId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            PushEnabled = true;
            PollActivityEnabled = true;
            OffersEnabled = true;
            AlertsEnabled = true;
            NormalEnabled = true;
            WhatsAppEnabled = true;
            SmsEnabled = true;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        public void Update(
            bool? pushEnabled,
            bool? pollActivityEnabled,
            bool? offersEnabled,
            bool? alertsEnabled,
            bool? normalEnabled,
            bool? whatsAppEnabled = null,
            bool? smsEnabled = null)
        {
            if (pushEnabled.HasValue)
            {
                PushEnabled = pushEnabled.Value;
            }

            if (pollActivityEnabled.HasValue)
            {
                PollActivityEnabled = pollActivityEnabled.Value;
            }

            if (offersEnabled.HasValue)
            {
                OffersEnabled = offersEnabled.Value;
            }

            if (alertsEnabled.HasValue)
            {
                AlertsEnabled = alertsEnabled.Value;
            }

            if (normalEnabled.HasValue)
            {
                NormalEnabled = normalEnabled.Value;
            }

            if (whatsAppEnabled.HasValue)
            {
                WhatsAppEnabled = whatsAppEnabled.Value;
            }

            if (smsEnabled.HasValue)
            {
                SmsEnabled = smsEnabled.Value;
            }

            UpdatedAt = DateTime.UtcNow;
        }
    }
}
