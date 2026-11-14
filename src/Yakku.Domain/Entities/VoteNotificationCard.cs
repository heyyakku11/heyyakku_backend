using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Durable notification state of one card. <see cref="VoteCount"/> counts the votes that notify its asker (the
    /// asker's own vote is excluded). It is bumped with a single atomic upsert inside the vote transaction, which also
    /// serializes concurrent votes on the card, so the "first three votes are immediate" rule never depends on Redis.
    /// <see cref="DeliveryMode"/> is the last delivery mode the frequency engine chose; Redis holds the live copy.
    /// </summary>
    public class VoteNotificationCard
    {
        public ShareContentType ContentType { get; private set; }
        public Guid ContentId { get; private set; }
        public Guid OwnerId { get; private set; }
        public int VoteCount { get; private set; }
        public VoteDeliveryMode DeliveryMode { get; private set; }
        public DateTime? LastVoteAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private VoteNotificationCard()
        {
        }
    }
}
