using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// An internal purchase order. Prices come from the plans catalog at creation time and are snapshotted so later
    /// catalog edits do not rewrite historical purchases. Razorpay order id is filled after the gateway accepts it.
    /// </summary>
    public class PaymentOrder
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid? PlanId { get; private set; }

        /// <summary>Poll or check when <see cref="PurchaseType"/> is <see cref="PaymentPurchaseType.CardUnlock"/>.</summary>
        public ShareContentType? ContentType { get; private set; }

        public Guid? ContentId { get; private set; }

        /// <summary>The ask back conversation a top-up adds questions to; set only for a conversation top-up.</summary>
        public Guid? ConversationId { get; private set; }
        public PaymentPurchaseType PurchaseType { get; private set; }
        public int AmountInPaise { get; private set; }
        public string Currency { get; private set; } = "INR";
        public string? RazorpayOrderId { get; private set; }
        public PaymentOrderStatus Status { get; private set; }

        /// <summary>JSON snapshot of the purchased plan row (code, price, duration, perks) at order time.</summary>
        public string PlanSnapshot { get; private set; } = "{}";

        /// <summary>Client-supplied key; unique per user so retries reuse the same order.</summary>
        public string IdempotencyKey { get; private set; } = string.Empty;

        public string? CorrelationId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? PaidAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        public ICollection<PaymentAttempt> Attempts { get; private set; } = new List<PaymentAttempt>();

        private PaymentOrder()
        {
        }

        public static PaymentOrder CreateUserPlan(
            Guid userId,
            Guid planId,
            int amountInPaise,
            string currency,
            string planSnapshotJson,
            string idempotencyKey,
            DateTime utcNow,
            TimeSpan lifetime,
            string? correlationId = null)
        {
            return CreateCore(
                userId,
                planId,
                contentType: null,
                contentId: null,
                PaymentPurchaseType.UserPlan,
                amountInPaise,
                currency,
                planSnapshotJson,
                idempotencyKey,
                utcNow,
                lifetime,
                correlationId);
        }

        public static PaymentOrder CreateCardUnlock(
            Guid userId,
            Guid planId,
            ShareContentType contentType,
            Guid contentId,
            int amountInPaise,
            string currency,
            string planSnapshotJson,
            string idempotencyKey,
            DateTime utcNow,
            TimeSpan lifetime,
            string? correlationId = null)
        {
            if (contentType is not (ShareContentType.Poll or ShareContentType.Check))
            {
                throw new ArgumentException("Only a poll or a check can be unlocked.", nameof(contentType));
            }

            return CreateCore(
                userId,
                planId,
                contentType,
                contentId,
                PaymentPurchaseType.CardUnlock,
                amountInPaise,
                currency,
                planSnapshotJson,
                idempotencyKey,
                utcNow,
                lifetime,
                correlationId);
        }

        public static PaymentOrder CreateConversationTopUp(
            Guid userId,
            Guid planId,
            Guid conversationId,
            int amountInPaise,
            string currency,
            string planSnapshotJson,
            string idempotencyKey,
            DateTime utcNow,
            TimeSpan lifetime,
            string? correlationId = null)
        {
            if (conversationId == Guid.Empty)
            {
                throw new ArgumentException("Conversation id is required.", nameof(conversationId));
            }

            var order = CreateCore(
                userId,
                planId,
                contentType: null,
                contentId: null,
                PaymentPurchaseType.ConversationTopUp,
                amountInPaise,
                currency,
                planSnapshotJson,
                idempotencyKey,
                utcNow,
                lifetime,
                correlationId);
            order.ConversationId = conversationId;
            return order;
        }

        public void AttachRazorpayOrder(string razorpayOrderId, DateTime utcNow, TimeSpan? renewLifetime = null)
        {
            if (string.IsNullOrWhiteSpace(razorpayOrderId))
            {
                throw new ArgumentException("Razorpay order id is required.", nameof(razorpayOrderId));
            }

            RazorpayOrderId = razorpayOrderId.Trim();
            if (Status is PaymentOrderStatus.Created or PaymentOrderStatus.Failed or PaymentOrderStatus.Expired)
            {
                Status = PaymentOrderStatus.Pending;
            }

            if (renewLifetime is { } lifetime && lifetime > TimeSpan.Zero)
            {
                ExpiresAt = utcNow.Add(lifetime);
            }

            UpdatedAt = utcNow;
        }

        public void MarkPaid(DateTime utcNow)
        {
            Status = PaymentOrderStatus.Paid;
            PaidAt = utcNow;
            UpdatedAt = utcNow;
        }

        public void MarkFailed(DateTime utcNow)
        {
            Status = PaymentOrderStatus.Failed;
            UpdatedAt = utcNow;
        }

        public void MarkCancelled(DateTime utcNow)
        {
            Status = PaymentOrderStatus.Cancelled;
            UpdatedAt = utcNow;
        }

        public void MarkExpired(DateTime utcNow)
        {
            Status = PaymentOrderStatus.Expired;
            UpdatedAt = utcNow;
        }

        private static PaymentOrder CreateCore(
            Guid userId,
            Guid planId,
            ShareContentType? contentType,
            Guid? contentId,
            PaymentPurchaseType purchaseType,
            int amountInPaise,
            string currency,
            string planSnapshotJson,
            string idempotencyKey,
            DateTime utcNow,
            TimeSpan lifetime,
            string? correlationId)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            if (planId == Guid.Empty)
            {
                throw new ArgumentException("Plan id is required.", nameof(planId));
            }

            if (amountInPaise < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountInPaise));
            }

            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentException("Currency is required.", nameof(currency));
            }

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
            }

            if (lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetime));
            }

            return new PaymentOrder
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = planId,
                ContentType = contentType,
                ContentId = contentId,
                PurchaseType = purchaseType,
                AmountInPaise = amountInPaise,
                Currency = currency.Trim().ToUpperInvariant(),
                Status = PaymentOrderStatus.Created,
                PlanSnapshot = string.IsNullOrWhiteSpace(planSnapshotJson) ? "{}" : planSnapshotJson,
                IdempotencyKey = idempotencyKey.Trim(),
                CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow,
                ExpiresAt = utcNow.Add(lifetime)
            };
        }
    }
}
