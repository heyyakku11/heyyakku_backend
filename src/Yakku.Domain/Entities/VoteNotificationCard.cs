using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Counts the votes of one card that notify its asker (the asker's own vote is excluded). Bumped with a single
    /// atomic upsert inside the vote transaction, which also serializes concurrent votes on the card.
    /// </summary>
    public class VoteNotificationCard
    {
        public ShareContentType ContentType { get; private set; }
        public Guid ContentId { get; private set; }
        public Guid OwnerId { get; private set; }
        public int VoteCount { get; private set; }

        private VoteNotificationCard()
        {
        }
    }
}
