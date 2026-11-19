using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Durable inbox row for a Razorpay webhook. Signature is verified before insert; processing is idempotent via
    /// the unique Razorpay event id.
    /// </summary>
    public class WebhookEvent
    {
        public Guid Id { get; private set; }
        public string RazorpayEventId { get; private set; } = string.Empty;
        public string EventType { get; private set; } = string.Empty;
        public string Payload { get; private set; } = "{}";
        public WebhookProcessingStatus ProcessingStatus { get; private set; }
        public DateTime ReceivedAt { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public string? ErrorMessage { get; private set; }
        public int RetryCount { get; private set; }

        private WebhookEvent()
        {
        }

        public static WebhookEvent Receive(
            string razorpayEventId,
            string eventType,
            string rawPayload,
            DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(razorpayEventId))
            {
                throw new ArgumentException("Razorpay event id is required.", nameof(razorpayEventId));
            }

            if (string.IsNullOrWhiteSpace(eventType))
            {
                throw new ArgumentException("Event type is required.", nameof(eventType));
            }

            if (string.IsNullOrWhiteSpace(rawPayload))
            {
                throw new ArgumentException("Payload is required.", nameof(rawPayload));
            }

            return new WebhookEvent
            {
                Id = Guid.NewGuid(),
                RazorpayEventId = razorpayEventId.Trim(),
                EventType = eventType.Trim(),
                Payload = rawPayload,
                ProcessingStatus = WebhookProcessingStatus.Received,
                ReceivedAt = utcNow,
                RetryCount = 0
            };
        }

        public void MarkProcessing()
        {
            ProcessingStatus = WebhookProcessingStatus.Processing;
            ProcessedAt = null;
        }

        public void MarkProcessed(DateTime utcNow)
        {
            ProcessingStatus = WebhookProcessingStatus.Processed;
            ProcessedAt = utcNow;
            ErrorMessage = null;
        }

        public void MarkFailed(string? errorMessage)
        {
            ProcessingStatus = WebhookProcessingStatus.Failed;
            RetryCount += 1;
            ProcessedAt = null;
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                ErrorMessage = null;
                return;
            }

            var trimmed = errorMessage.Trim();
            ErrorMessage = trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
        }
    }
}
