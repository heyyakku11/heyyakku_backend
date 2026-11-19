using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A poll or check whose owner may ask back on every reason. There is at most one per card and owner. The card
    /// is not a foreign key (like the conversation's), so a deleted card does not need a cascade here.
    /// Plan unlocks may reference the entitlement that spent the ticket; purchase unlocks may reference the payment.
    /// </summary>
    public class CardUnlock
    {
        public Guid Id { get; private set; }
        public Guid OwnerUserId { get; private set; }
        public ShareContentType ContentType { get; private set; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; private set; }
        public CardUnlockSource Source { get; private set; }

        /// <summary>Set when unlocked by a paid plan ticket (or Love auto-unlock) under a specific entitlement.</summary>
        public Guid? UserPlanEntitlementId { get; private set; }

        /// <summary>Set when unlocked by a verified permanent-card payment (or promoted to purchase).</summary>
        public Guid? PaymentOrderId { get; private set; }

        public DateTime CreatedAt { get; private set; }

        private CardUnlock()
        {
        }

        /// <summary>
        /// A card bought on its own stays unlocked when a plan ends. Returns false when it already was a purchase.
        /// </summary>
        public bool PromoteToPurchase(Guid? paymentOrderId = null)
        {
            if (Source == CardUnlockSource.Purchase)
            {
                if (paymentOrderId is Guid orderId && PaymentOrderId is null)
                {
                    PaymentOrderId = orderId;
                    return true;
                }

                return false;
            }

            Source = CardUnlockSource.Purchase;
            if (paymentOrderId is Guid paidOrderId)
            {
                PaymentOrderId = paidOrderId;
            }

            return true;
        }

        public static CardUnlock Create(
            Guid ownerUserId,
            ShareContentType contentType,
            Guid contentId,
            CardUnlockSource source,
            DateTime utcNow,
            Guid? userPlanEntitlementId = null,
            Guid? paymentOrderId = null)
        {
            if (contentType == ShareContentType.Personality)
            {
                throw new ArgumentException("Only a poll or a check can be unlocked.", nameof(contentType));
            }

            if (source == CardUnlockSource.Plan && paymentOrderId is not null)
            {
                throw new ArgumentException("Plan unlocks do not attach a payment order.", nameof(paymentOrderId));
            }

            if (source == CardUnlockSource.Purchase && userPlanEntitlementId is not null)
            {
                throw new ArgumentException("Purchase unlocks do not attach a plan entitlement.", nameof(userPlanEntitlementId));
            }

            return new CardUnlock
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                ContentType = contentType,
                ContentId = contentId,
                Source = source,
                UserPlanEntitlementId = userPlanEntitlementId,
                PaymentOrderId = paymentOrderId,
                CreatedAt = utcNow
            };
        }
    }
}
