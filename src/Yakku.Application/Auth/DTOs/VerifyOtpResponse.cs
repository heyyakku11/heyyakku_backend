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

        /// <summary>The tokens for this session.</summary>
        public TokenResponse Auth { get; set; } = new();
    }

    /// <summary>What <see cref="Yakku.Application.Auth.Interfaces.ILoginSnapshotReader"/> returns.</summary>
    public sealed record LoginSnapshot(
        string CardShareCode,
        string CardStatus,
        string NotificationPermission,
        bool IsPushAllowed,
        bool CommunityNotifyRequested = false);
}
