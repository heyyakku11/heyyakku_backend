namespace Yakku.Application.Devices.DTOs
{
    /// <summary>
    /// Partial update of the caller's current installation. A missing / null field is left unchanged;
    /// an empty string clears it (for <c>pushToken</c> that disables push delivery). The installation id and
    /// platform are identity and cannot be changed.
    /// </summary>
    public class UpdateDeviceRequest
    {
        public string? PushToken { get; set; }
        public string? DeviceModel { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
        public string? AppBuild { get; set; }
        public string? Locale { get; set; }
        public string? Timezone { get; set; }
        public string? NotificationPermission { get; set; }

        /// <summary>
        /// The in-app push switch for this installation. It can only be turned on while the notification permission
        /// is granted; a newly granted permission turns it on, any other permission turns it off.
        /// </summary>
        public bool? IsPushAllowed { get; set; }
    }
}
