using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One row per delivery attempt. <see cref="Status"/> is what the provider said at hand-over;
    /// <see cref="DeliveryStatus"/> stays Unknown until the provider reports the real outcome.
    /// </summary>
    public class NotificationAuditLog
    {
        public Guid Id { get; private set; }
        public NotificationChannel Channel { get; private set; }
        public string Provider { get; private set; } = string.Empty;
        public string? EventType { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? DeviceId { get; private set; }
        public string? Recipient { get; private set; }
        public Guid? CorrelationId { get; private set; }
        public int AttemptNumber { get; private set; }
        public bool WillRetry { get; private set; }
        public NotificationAuditStatus Status { get; private set; }
        public NotificationDeliveryStatus DeliveryStatus { get; private set; }
        public string? ProviderMessageId { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? ErrorMessage { get; private set; }
        public int? LatencyMs { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ProviderAcceptedAt { get; private set; }
        public DateTime? DeliveryUpdatedAt { get; private set; }

        private NotificationAuditLog()
        {
        }

        public NotificationAuditLog(
            NotificationChannel channel,
            string provider,
            string? eventType,
            Guid? userId,
            Guid? deviceId,
            string? recipient,
            Guid? correlationId,
            int attemptNumber,
            bool willRetry,
            NotificationAuditStatus status,
            string? providerMessageId,
            string? errorCode,
            string? errorMessage,
            int? latencyMs,
            DateTime? createdAt = null)
        {
            var now = createdAt ?? DateTime.UtcNow;
            Id = Guid.NewGuid();
            Channel = channel;
            Provider = provider;
            EventType = eventType;
            UserId = userId;
            DeviceId = deviceId;
            Recipient = recipient;
            CorrelationId = correlationId;
            AttemptNumber = attemptNumber;
            WillRetry = willRetry;
            Status = status;
            DeliveryStatus = status == NotificationAuditStatus.Accepted
                ? NotificationDeliveryStatus.Unknown
                : NotificationDeliveryStatus.NotApplicable;
            ProviderMessageId = providerMessageId;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            LatencyMs = latencyMs;
            CreatedAt = now;
            ProviderAcceptedAt = status == NotificationAuditStatus.Accepted ? now : null;
        }

        /// <summary>Applies a provider delivery report. Only an accepted attempt can later be delivered or fail delivery.</summary>
        public bool ApplyDeliveryReport(
            NotificationDeliveryStatus deliveryStatus,
            DateTime occurredAt,
            string? errorCode,
            string? errorMessage)
        {
            if (Status != NotificationAuditStatus.Accepted
                || deliveryStatus is NotificationDeliveryStatus.Unknown or NotificationDeliveryStatus.NotApplicable)
            {
                return false;
            }

            DeliveryStatus = deliveryStatus;
            DeliveryUpdatedAt = occurredAt;
            if (deliveryStatus == NotificationDeliveryStatus.Failed)
            {
                ErrorCode = errorCode;
                ErrorMessage = errorMessage;
            }

            return true;
        }
    }
}
