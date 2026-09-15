namespace Yakku.Application.Devices.DTOs
{
    public class DeviceResponse
    {
        public string InstallationId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
        public string? NotificationPermission { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastSeenAt { get; set; }
    }
}
