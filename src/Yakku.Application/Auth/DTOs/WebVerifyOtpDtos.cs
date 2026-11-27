namespace Yakku.Application.Auth.DTOs
{
    /// <summary>Website login. Carries only the phone number and code; there is no device or installation.</summary>
    public class WebVerifyOtpRequest
    {
        public string PhoneNumber { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
    }

    /// <summary>
    /// What the website gets after signing in: one access token and how long it lives. There is no refresh token;
    /// when the token ends, the visitor signs in again.
    /// </summary>
    public class WebVerifyOtpResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Seconds until the access token expires (always 900).</summary>
        public int ExpiresInSeconds { get; set; }

        /// <summary>True when this verify created the account, false when an existing account signed in.</summary>
        public bool IsNewUser { get; set; }

        /// <summary>
        /// What happened to the visitor's guest identity: "claimed" (its votes now belong to the account),
        /// "none" (no usable guest token, nothing to move) or "failed" (nothing changed, the guest is kept for the next
        /// sign-in). Unless it is "failed", the browser should forget its guest token.
        /// </summary>
        public string GuestClaim { get; set; } = "none";
    }
}
