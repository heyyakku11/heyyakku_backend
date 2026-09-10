namespace Yakku.Application.Devices.DTOs
{
    public class UpdateDeviceRequest
    {
        public string? PushToken { get; set; }
        public string? Platform { get; set; }
        public string? DeviceModel { get; set; }
        public string? OsVersion { get; set; }
        public string? AppVersion { get; set; }
        public string? AppBuild { get; set; }
        public string? Locale { get; set; }
        public string? Timezone { get; set; }
        public string? NotificationPermission { get; set; }
        public bool? IsActive { get; set; }
    }
}
