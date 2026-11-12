using System.Net;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A server-side admin login session. The browser holds only an opaque random token in an HttpOnly cookie;
    /// this row stores its SHA-256 hash, never the token itself. A session ends by idle timeout, absolute timeout
    /// or revocation, and a revoked row never authenticates again.
    /// </summary>
    public class AdminSession
    {
        public Guid Id { get; private set; }
        public Guid AdminId { get; private set; }

        /// <summary>SHA-256 (hex) of the current session token.</summary>
        public string SessionHash { get; private set; } = string.Empty;

        /// <summary>
        /// Hash of the token before the last rotation, accepted only until <see cref="PreviousHashValidUntil"/> so
        /// requests already in flight during a rotation do not fail.
        /// </summary>
        public string? PreviousSessionHash { get; private set; }
        public DateTime? PreviousHashValidUntil { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime LastActivityAt { get; private set; }
        public DateTime RotatedAt { get; private set; }

        /// <summary>The session ends if unused past this moment. Each use pushes it forward, never past the absolute limit.</summary>
        public DateTime IdleExpiresAt { get; private set; }

        /// <summary>The session ends here no matter how active it is.</summary>
        public DateTime AbsoluteExpiresAt { get; private set; }

        public DateTime? RevokedAt { get; private set; }
        public string? RevocationReason { get; private set; }

        public IPAddress? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }

        public Admin Admin { get; private set; } = null!;

        private AdminSession()
        {
        }

        public AdminSession(
            Guid adminId,
            string sessionHash,
            DateTime now,
            DateTime idleExpiresAt,
            DateTime absoluteExpiresAt,
            IPAddress? ipAddress = null,
            string? userAgent = null)
        {
            Id = Guid.NewGuid();
            AdminId = adminId;
            SessionHash = sessionHash;
            CreatedAt = now;
            LastActivityAt = now;
            RotatedAt = now;
            IdleExpiresAt = idleExpiresAt;
            AbsoluteExpiresAt = absoluteExpiresAt;
            IpAddress = ipAddress;
            UserAgent = userAgent;
        }

        public bool IsRevoked => RevokedAt is not null;

        public bool IsExpired(DateTime utcNow) => utcNow >= IdleExpiresAt || utcNow >= AbsoluteExpiresAt;
    }
}
