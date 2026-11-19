using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One paid plan access period for a user. Free is never stored here — it is the default when no active paid
    /// entitlement exists. History is preserved; a Lite row superseded by Love is never reactivated.
    /// </summary>
    public class UserPlanEntitlement
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid PlanId { get; private set; }

        /// <summary>Null when activated by an admin override rather than a captured payment.</summary>
        public Guid? PaymentOrderId { get; private set; }

        public UserPlanEntitlementStatus Status { get; private set; }
        public DateTime StartsAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? SupersededAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        private UserPlanEntitlement()
        {
        }

        public static UserPlanEntitlement Activate(
            Guid userId,
            Guid planId,
            DateTime startsAt,
            DateTime expiresAt,
            Guid? paymentOrderId = null)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            if (planId == Guid.Empty || planId == Plan.FreeId)
            {
                throw new ArgumentException("A paid plan id is required.", nameof(planId));
            }

            if (expiresAt <= startsAt)
            {
                throw new ArgumentException("ExpiresAt must be after StartsAt.");
            }

            return new UserPlanEntitlement
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = planId,
                PaymentOrderId = paymentOrderId,
                Status = UserPlanEntitlementStatus.Active,
                StartsAt = startsAt,
                ExpiresAt = expiresAt,
                CreatedAt = startsAt,
                UpdatedAt = startsAt
            };
        }

        public void MarkExpired(DateTime utcNow)
        {
            if (Status != UserPlanEntitlementStatus.Active)
            {
                return;
            }

            Status = UserPlanEntitlementStatus.Expired;
            UpdatedAt = utcNow;
        }

        public void MarkSuperseded(DateTime utcNow)
        {
            if (Status != UserPlanEntitlementStatus.Active)
            {
                return;
            }

            Status = UserPlanEntitlementStatus.Superseded;
            SupersededAt = utcNow;
            UpdatedAt = utcNow;
        }

        public void MarkRevoked(DateTime utcNow)
        {
            Status = UserPlanEntitlementStatus.Revoked;
            UpdatedAt = utcNow;
        }

        public bool IsActiveAt(DateTime utcNow)
        {
            return Status == UserPlanEntitlementStatus.Active && ExpiresAt > utcNow;
        }
    }
}
