namespace Yakku.Application.Users.DTOs
{
    /// <summary>What the signed-in user allows.</summary>
    public class UserPermissionsResponse
    {
        /// <summary>Whether people the user answers may ask them back. On by default.</summary>
        public bool AllowAskBack { get; set; } = true;

        /// <summary>Whether the user wants push notifications. Off by default.</summary>
        public bool PushNotification { get; set; }

        /// <summary>Whether the user wants SMS messages. On by default.</summary>
        public bool Sms { get; set; } = true;

        /// <summary>Whether the user wants WhatsApp messages. On by default.</summary>
        public bool WhatsApp { get; set; } = true;
    }

    /// <summary>A partial update: only the permissions that are sent change.</summary>
    public class UpdateUserPermissionsRequest
    {
        public bool? AllowAskBack { get; set; }
        public bool? PushNotification { get; set; }
        public bool? Sms { get; set; }
        public bool? WhatsApp { get; set; }
    }
}
