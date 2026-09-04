namespace Yakku.Domain.Entities
{
    public class UserSession
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string TokenHash { get; private set; } = string.Empty;
        public string TokenSalt { get; private set; } = string.Empty;
        public DateTime ExpiresAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private UserSession()
        {
        }

        public UserSession(Guid userId, string tokenHash, string tokenSalt, DateTime expiresAt)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            TokenHash = tokenHash;
            TokenSalt = tokenSalt;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Rotate(string tokenHash, string tokenSalt, DateTime expiresAt)
        {
            TokenHash = tokenHash;
            TokenSalt = tokenSalt;
            ExpiresAt = expiresAt;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool IsExpired(DateTime utcNow)
        {
            return utcNow >= ExpiresAt;
        }
    }
}
