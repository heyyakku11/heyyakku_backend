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

        /// <summary>True when this verify created the account, false when an existing account signed in.</summary>
        public bool IsNewUser { get; set; }

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
