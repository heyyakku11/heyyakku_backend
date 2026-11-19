using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Long-lived payment lifecycle audit row. Never stores API secrets, webhook secrets, authorization headers,
    /// PAN, CVV or other payment credentials.
    /// </summary>
    public class PaymentLog
    {
        public Guid Id { get; private set; }
        public Guid? PaymentOrderId { get; private set; }
        public Guid? PaymentAttemptId { get; private set; }
        public Guid? UserId { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public string EventSource { get; private set; } = string.Empty;
        public PaymentLogStatus Status { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public string? CorrelationId { get; private set; }
        public string? RazorpayOrderId { get; private set; }
        public string? RazorpayPaymentId { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private PaymentLog()
        {
        }

        public static PaymentLog Create(
            string eventType,
            string eventSource,
            PaymentLogStatus status,
            string message,
            DateTime utcNow,
            Guid? paymentOrderId = null,
            Guid? paymentAttemptId = null,
            Guid? userId = null,
            string? correlationId = null,
            string? razorpayOrderId = null,
            string? razorpayPaymentId = null,
            string? metadataJson = null)
        {
            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException("Event type is required.", nameof(eventType));
            }

            if (string.IsNullOrWhiteSpace(eventSource))
            {
                throw new ArgumentException("Event source is required.", nameof(eventSource));
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message is required.", nameof(message));
            }

            return new PaymentLog
            {
                Id = Guid.NewGuid(),
                PaymentOrderId = paymentOrderId,
                PaymentAttemptId = paymentAttemptId,
                UserId = userId,
                EventType = eventType.Trim(),
                EventSource = eventSource.Trim(),
                Status = status,
                Message = message.Trim(),
                CorrelationId = NormalizeOptional(correlationId),
                RazorpayOrderId = NormalizeOptional(razorpayOrderId),
                RazorpayPaymentId = NormalizeOptional(razorpayPaymentId),
                Metadata = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson,
                CreatedAt = utcNow
            };
        }

        private static string? NormalizeOptional(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
