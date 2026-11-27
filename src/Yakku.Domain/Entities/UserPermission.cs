namespace Yakku.Domain.Entities
{
    /// <summary>
    /// What the user allows, one row per user (1:1 with <see cref="User"/>, keyed by the user id). The row is created
    /// together with the account, so every user has one.
    /// </summary>
    public class UserPermission
    {
        public Guid UserId { get; private set; }

        /// <summary>
        /// "Let people I answer ask me back": when false, nobody can start an Ask Back conversation with this user
        /// about a vote they cast. On by default.
        /// </summary>
        public bool AllowAskBack { get; private set; } = true;

        /// <summary>Whether the user wants push notifications. Off by default.</summary>
        public bool PushNotification { get; private set; }

        /// <summary>Whether the user wants SMS messages. On by default.</summary>
        public bool Sms { get; private set; } = true;

        /// <summary>Whether the user wants WhatsApp messages. On by default.</summary>
        public bool WhatsApp { get; private set; } = true;

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private UserPermission()
        {
        }

        public UserPermission(Guid userId)
        {
            UserId = userId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        /// <summary>Changes only the permissions that are given; the others keep their value.</summary>
        public void Apply(bool? allowAskBack, bool? pushNotification, bool? sms, bool? whatsApp, DateTime utcNow)
        {
            if (allowAskBack is { } askBack)
            {
                AllowAskBack = askBack;
            }

            if (pushNotification is { } push)
            {
                PushNotification = push;
            }

            if (sms is { } sendSms)
            {
                Sms = sendSms;
            }

            if (whatsApp is { } sendWhatsApp)
            {
                WhatsApp = sendWhatsApp;
            }

            UpdatedAt = utcNow;
        }
    }
}
