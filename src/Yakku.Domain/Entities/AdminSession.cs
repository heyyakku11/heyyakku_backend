using System.Net;

namespace Yakku.Domain.Entities
{
    public class AdminSession
    {
        public Guid Id { get; private set; }
        public Guid AdminId { get; private set; }
        public string RefreshTokenHash { get; private set; } = string.Empty;
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public IPAddress? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Admin Admin { get; private set; } = null!;

        private AdminSession()
        {
        }

        public AdminSession(
            Guid adminId,
            string refreshTokenHash,
            DateTime expiresAt,
            IPAddress? ipAddress = null,
            string? userAgent = null)
        {
            Id = Guid.NewGuid();
            AdminId = adminId;
            RefreshTokenHash = refreshTokenHash;
            ExpiresAt = expiresAt;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            CreatedAt = DateTime.UtcNow;
        }

        public void Rotate(string refreshTokenHash, DateTime expiresAt)
        {
            RefreshTokenHash = refreshTokenHash;
            ExpiresAt = expiresAt;
            LastUsedAt = DateTime.UtcNow;
        }

        public bool IsExpired(DateTime utcNow)
        {
            return utcNow >= ExpiresAt;
        }
    }
}
