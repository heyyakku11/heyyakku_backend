namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A user who asked to be told when the community goes live. One entry per user; the first to ask is
    /// the first in the queue. <see cref="NotifiedAt"/> is set once they have been told.
    /// </summary>
    public class CommunityWaitlistEntry
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        /// <summary>When the "community is live" notification was sent; null while they are still waiting.</summary>
        public DateTime? NotifiedAt { get; private set; }

        public User User { get; private set; } = null!;

        private CommunityWaitlistEntry()
        {
        }

        public static CommunityWaitlistEntry Create(Guid userId)
        {
            return new CommunityWaitlistEntry
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
