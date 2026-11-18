using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A poll or check whose owner may ask back on every reason. There is at most one per card and owner. The card
    /// is not a foreign key (like the conversation's), so a deleted card does not need a cascade here.
    /// </summary>
    public class CardUnlock
    {
        public Guid Id { get; private set; }
        public Guid OwnerUserId { get; private set; }
        public ShareContentType ContentType { get; private set; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; private set; }
        public CardUnlockSource Source { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private CardUnlock()
        {
        }

        /// <summary>
        /// A card bought on its own stays unlocked when a plan ends. Returns false when it already was a purchase.
        /// </summary>
        public bool PromoteToPurchase()
        {
            if (Source == CardUnlockSource.Purchase)
            {
                return false;
            }

            Source = CardUnlockSource.Purchase;
            return true;
        }

        public static CardUnlock Create(
            Guid ownerUserId,
            ShareContentType contentType,
            Guid contentId,
            CardUnlockSource source,
            DateTime utcNow)
        {
            if (contentType == ShareContentType.Personality)
            {
                throw new ArgumentException("Only a poll or a check can be unlocked.", nameof(contentType));
            }

            return new CardUnlock
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                ContentType = contentType,
                ContentId = contentId,
                Source = source,
                CreatedAt = utcNow
            };
        }
    }
}
