namespace Yakku.Application.Auth.DTOs
{
    /// <summary>
    /// The installation that is signing in, sent with the OTP. Only what is needed to bind the new session to the
    /// installation. Notification permission and the push token are sent afterwards, once the user has answered the
    /// OS prompt, with PATCH /api/v1/devices/me; other metadata (model, versions, locale) goes there too.
    /// </summary>
    public class LoginDeviceRequest
    {
        public string InstallationId { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }
}
