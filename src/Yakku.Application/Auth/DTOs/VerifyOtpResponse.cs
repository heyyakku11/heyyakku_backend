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
        public string AnonymousName { get; set; } = string.Empty;

        /// <summary>The user's personality card: its share code and where it is in its lifecycle.</summary>
        public VerifyOtpPersonalityCard PersonalityCard { get; set; } = new();

        /// <summary>Permission state of the device that signed in.</summary>
        public VerifyOtpPermissions Permissions { get; set; } = new();
    }

    public class VerifyOtpPersonalityCard
    {
        /// <summary>The code in the card's share link.</summary>
        public string ShareCode { get; set; } = string.Empty;

        /// <summary>Draft, Collecting or Completed.</summary>
        public string Status { get; set; } = string.Empty;
    }

    public class VerifyOtpPermissions
    {
        /// <summary>Unknown, Granted or Denied. Unknown when no device was sent or it never reported one.</summary>
        public string Notification { get; set; } = "Unknown";

        /// <summary>Whether push is switched on for this device.</summary>
        public bool IsPushAllowed { get; set; }
    }

    /// <summary>What <see cref="Yakku.Application.Auth.Interfaces.ILoginSnapshotReader"/> returns.</summary>
    public sealed record LoginSnapshot(
        string CardShareCode,
        string CardStatus,
        string NotificationPermission,
        bool IsPushAllowed);
}
