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
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;

        /// <summary>True when this login cancelled a pending account deletion.</summary>
        public bool AccountRestored { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public int AccessTokenExpiresInSeconds { get; set; }
        public int RefreshTokenExpiresInSeconds { get; set; }
    }
}
