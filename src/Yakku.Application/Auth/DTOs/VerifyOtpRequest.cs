using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Auth.DTOs
{
    public class VerifyOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;

        /// <summary>
        /// Optional. The app installation that is signing in. When present, the new session and the installation
        /// are bound atomically, so push goes to this account on this installation from the first moment and
        /// never to the account that used it before. Omit it to bind later through POST /devices/register.
        /// </summary>
        public RegisterDeviceRequest? Device { get; set; }
    }
}
