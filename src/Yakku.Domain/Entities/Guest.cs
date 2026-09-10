namespace Yakku.Domain.Entities
{
    public class Guest
    {
        public Guid Id { get; private set; }
        public string GuestTokenHash { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastSeenAt { get; private set; }
        public DateTime? ExpiresAt { get; private set; }

        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();
        public ICollection<SystemLog> SystemLogs { get; private set; } = new List<SystemLog>();

        private Guest()
        {
        }

        public Guest(string guestTokenHash)
        {
            Id = Guid.NewGuid();
            GuestTokenHash = guestTokenHash;
            CreatedAt = DateTime.UtcNow;
            LastSeenAt = CreatedAt;
        }

        public void Touch()
        {
            LastSeenAt = DateTime.UtcNow;
        }
    }
}
