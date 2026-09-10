namespace Yakku.Application.Devices.DTOs
{
    public class RegisterDeviceResult
    {
        public DeviceResponse Device { get; set; } = null!;
        public bool Created { get; set; }
    }
}
