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
        {
            Id = Guid.NewGuid();
            GuestTokenHash = guestTokenHash;
            CreatedAt = DateTime.UtcNow;
            LastSeenAt = CreatedAt;
            ExpiresAt = CreatedAt.AddDays(7);
        }

        public void Touch()
        {
            LastSeenAt = DateTime.UtcNow;
        }
    }
}
