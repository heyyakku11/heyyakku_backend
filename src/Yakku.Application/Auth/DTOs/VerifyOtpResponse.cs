using System.Text.Json.Serialization;

namespace Yakku.Application.Auth.DTOs
{
    public class VerifyOtpResponse
    {
        /// <summary>
        /// For server-side use only (guest claim, audit): never sent to the client. The access token already carries
        /// the user id as its subject.
        /// </summary>
        [JsonIgnore]
        public Guid UserId { get; set; }

        /// <summary>"Registration" for a new account, "Login" for an existing one.</summary>
        public string Purpose { get; set; } = string.Empty;

        /// <summary>True when this login cancelled a pending account deletion.</summary>
        public bool AccountRestored { get; set; }

        /// <summary>Who signed in.</summary>
        public VerifyOtpUser User { get; set; } = new();

        /// <summary>The tokens for this session.</summary>
        public TokenResponse Auth { get; set; } = new();
    }

    public class VerifyOtpUser
    {
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;

        /// <summary>
        /// Whether the account allows push notifications (the in-app switch, on by default). This is not the phone's
        /// OS permission, which the app reports with PATCH /api/v1/devices/me.
        /// </summary>
        public bool IsPushAllowed { get; set; } = true;
    }
}
