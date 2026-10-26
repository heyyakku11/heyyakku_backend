namespace Yakku.Domain.Entities
{
    public class Guest
    {
        public Guid Id { get; private set; }
        public string GuestTokenHash { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastSeenAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        private Guest()
        {
        }

        public Guest(string guestTokenHash)
            : this(guestTokenHash, DateTime.UtcNow)
        {
        }

        public Guest(string guestTokenHash, DateTime createdAtUtc)
        {
            Id = Guid.NewGuid();
            GuestTokenHash = guestTokenHash;
            CreatedAt = createdAtUtc;
            LastSeenAt = CreatedAt;
            ExpiresAt = CreatedAt.AddDays(30);
        }

        public bool IsExpired(DateTime utcNow)
        {
            return ExpiresAt <= utcNow;
        }

        public void Touch()
        {
            LastSeenAt = DateTime.UtcNow;
        }
    }
}
