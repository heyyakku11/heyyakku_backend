using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Admin-initiated Razorpay refund against a captured payment attempt. Does not automatically revoke
    /// entitlements or permanent unlocks — access changes require an explicit separate admin action.
    /// </summary>
    public class PaymentRefund
    {
        public Guid Id { get; private set; }
        public Guid PaymentAttemptId { get; private set; }
        public Guid PaymentOrderId { get; private set; }
        public string? RazorpayRefundId { get; private set; }
        public int AmountInPaise { get; private set; }
        public PaymentRefundStatus Status { get; private set; }
        public string? Reason { get; private set; }
        public string? FailureCode { get; private set; }
        public string? FailureReason { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }

        public PaymentAttempt PaymentAttempt { get; private set; } = null!;

        private PaymentRefund()
        {
        }

        public static PaymentRefund Request(
            Guid paymentAttemptId,
            Guid paymentOrderId,
            int amountInPaise,
            string? reason,
            DateTime utcNow)
        {
            if (paymentAttemptId == Guid.Empty)
            {
                throw new ArgumentException("Payment attempt id is required.", nameof(paymentAttemptId));
            }

            if (paymentOrderId == Guid.Empty)
            {
                throw new ArgumentException("Payment order id is required.", nameof(paymentOrderId));
            }

            if (amountInPaise <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountInPaise));
            }

            return new PaymentRefund
            {
                Id = Guid.NewGuid(),
                PaymentAttemptId = paymentAttemptId,
                PaymentOrderId = paymentOrderId,
                AmountInPaise = amountInPaise,
                Status = PaymentRefundStatus.Requested,
                Reason = NormalizeOptional(reason),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
        }

        public void MarkProcessed(string razorpayRefundId, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(razorpayRefundId))
            {
                throw new ArgumentException("Razorpay refund id is required.", nameof(razorpayRefundId));
            }

            RazorpayRefundId = razorpayRefundId.Trim();
            Status = PaymentRefundStatus.Processed;
            ProcessedAt = utcNow;
            UpdatedAt = utcNow;
            FailureCode = null;
            FailureReason = null;
        }

        public void MarkFailed(string? failureCode, string? failureReason, DateTime utcNow)
        {
            Status = PaymentRefundStatus.Failed;
            FailureCode = NormalizeOptional(failureCode);
            FailureReason = NormalizeOptional(failureReason);
            UpdatedAt = utcNow;
        }

        private static string? NormalizeOptional(string? value)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return null;
            }

            return trimmed.Length <= 500 ? trimmed : trimmed[..500];
        }
    }
}
