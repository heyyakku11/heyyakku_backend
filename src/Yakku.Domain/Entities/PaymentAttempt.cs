using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One gateway payment attempt for an internal order. Never stores PAN, CVV, UPI PIN or bank credentials —
    /// only non-sensitive Razorpay metadata.
    /// </summary>
    public class PaymentAttempt
    {
        public Guid Id { get; private set; }
        public Guid PaymentOrderId { get; private set; }
        public string? RazorpayPaymentId { get; private set; }
        public string? PaymentMethod { get; private set; }
        public PaymentAttemptStatus Status { get; private set; }
        public int AmountInPaise { get; private set; }
        public string Currency { get; private set; } = "INR";
        public string? RazorpayStatus { get; private set; }
        public string? FailureCode { get; private set; }
        public string? FailureReason { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? AuthorizedAt { get; private set; }
        public DateTime? CapturedAt { get; private set; }
        public DateTime? FailedAt { get; private set; }

        public PaymentOrder PaymentOrder { get; private set; } = null!;

        private PaymentAttempt()
        {
        }

        public static PaymentAttempt Create(
            Guid paymentOrderId,
            int amountInPaise,
            string currency,
            DateTime utcNow,
            string? razorpayPaymentId = null,
            string? paymentMethod = null,
            string? razorpayStatus = null)
        {
            if (paymentOrderId == Guid.Empty)
            {
                throw new ArgumentException("Payment order id is required.", nameof(paymentOrderId));
            }

            if (amountInPaise < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountInPaise));
            }

            return new PaymentAttempt
            {
                Id = Guid.NewGuid(),
                PaymentOrderId = paymentOrderId,
                RazorpayPaymentId = NormalizeOptional(razorpayPaymentId),
                PaymentMethod = NormalizeOptional(paymentMethod),
                Status = PaymentAttemptStatus.Created,
                AmountInPaise = amountInPaise,
                Currency = currency.Trim().ToUpperInvariant(),
                RazorpayStatus = NormalizeOptional(razorpayStatus),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
        }

        public void MarkAuthorized(DateTime utcNow, string? razorpayStatus = null, string? paymentMethod = null)
        {
            Status = PaymentAttemptStatus.Authorized;
            AuthorizedAt = utcNow;
            UpdatedAt = utcNow;
            if (razorpayStatus is not null)
            {
                RazorpayStatus = NormalizeOptional(razorpayStatus);
            }

            if (paymentMethod is not null)
            {
                PaymentMethod = NormalizeOptional(paymentMethod);
            }
        }

        public void MarkCaptured(DateTime utcNow, string? razorpayStatus = null, string? paymentMethod = null)
        {
            Status = PaymentAttemptStatus.Captured;
            CapturedAt = utcNow;
            AuthorizedAt ??= utcNow;
            UpdatedAt = utcNow;
            if (razorpayStatus is not null)
            {
                RazorpayStatus = NormalizeOptional(razorpayStatus);
            }

            if (paymentMethod is not null)
            {
                PaymentMethod = NormalizeOptional(paymentMethod);
            }
        }

        public void MarkFailed(
            DateTime utcNow,
            string? failureCode,
            string? failureReason,
            string? razorpayStatus = null)
        {
            Status = PaymentAttemptStatus.Failed;
            FailedAt = utcNow;
            UpdatedAt = utcNow;
            FailureCode = NormalizeOptional(failureCode);
            FailureReason = NormalizeOptional(failureReason);
            if (razorpayStatus is not null)
            {
                RazorpayStatus = NormalizeOptional(razorpayStatus);
            }
        }

        public void AttachRazorpayPaymentId(string razorpayPaymentId, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(razorpayPaymentId))
            {
                throw new ArgumentException("Razorpay payment id is required.", nameof(razorpayPaymentId));
            }

            RazorpayPaymentId = razorpayPaymentId.Trim();
            UpdatedAt = utcNow;
        }

        private static string? NormalizeOptional(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
