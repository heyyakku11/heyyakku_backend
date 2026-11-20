namespace Yakku.Domain.Entities
{
    /// <summary>1:1 password credential for an admin. Kept off the <see cref="Admin"/> row.</summary>
    public class AdminPassword
    {
        public Guid AdminId { get; private set; }
        public string PasswordHash { get; private set; } = string.Empty;
        public DateTime UpdatedAt { get; private set; }

        public Admin Admin { get; private set; } = null!;

        private AdminPassword()
        {
        }

        public static AdminPassword Create(Guid adminId, string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash is required.", nameof(passwordHash));
            }

            return new AdminPassword
            {
                AdminId = adminId,
                PasswordHash = passwordHash,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public void UpdateHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash is required.", nameof(passwordHash));
            }

            PasswordHash = passwordHash;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
