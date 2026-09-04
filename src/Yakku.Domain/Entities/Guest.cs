namespace Yakku.Domain.Entities
{
    public class Guest
    {
        public Guid Id { get; private set; }
        public string TokenHash { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime LastSeenAt { get; private set; }

        private Guest()
        {
        }

        public Guest(string tokenHash)
        {
            Id = Guid.NewGuid();
            TokenHash = tokenHash;
            CreatedAt = DateTime.UtcNow;
            LastSeenAt = CreatedAt;
        }

        public void Touch()
        {
            LastSeenAt = DateTime.UtcNow;
        }
    }
}
