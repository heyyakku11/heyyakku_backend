namespace Yakku.Domain.Entities
{
    public class UserSession
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid? DeviceId { get; private set; }
        public string RefreshTokenHash { get; private set; } = string.Empty;
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public DateTime? LastUsedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Device? Device { get; private set; }

        private UserSession()
        {
        }

        public UserSession(Guid userId, string refreshTokenHash, DateTime expiresAt)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            RefreshTokenHash = refreshTokenHash;
            ExpiresAt = expiresAt;
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
