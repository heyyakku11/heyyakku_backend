namespace Yakku.Application.Devices.DTOs
{
    /// <summary>
    /// Public view of an installation. The push token is never returned; <see cref="PushEnabled"/> says whether
    /// one is currently usable.
    /// </summary>
    public class DeviceResponse
    {
        public string InstallationId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string? DeviceModel { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
        public string? AppBuild { get; set; }
        public string? Locale { get; set; }
        public string? Timezone { get; set; }
        public string? NotificationPermission { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// Whether push is switched on for this installation. Per installation: off until the notification permission
        /// is granted here, and independent of the user's other devices.
        /// </summary>
        public bool IsPushAllowed { get; set; }

        /// <summary>True when push can be delivered right now (active, permitted and a token is stored).</summary>
        public bool PushEnabled { get; set; }

        /// <summary>True for the installation making this request. Only set by the list endpoint.</summary>
        public bool IsCurrent { get; set; }

        public DateTime? LastSeenAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
