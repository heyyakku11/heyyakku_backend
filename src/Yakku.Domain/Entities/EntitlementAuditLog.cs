using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>Append-only plan/unlock/access trail. Complements payment_logs. No FKs.</summary>
    public class EntitlementAuditLog
    {
        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public AuditOutcome Outcome { get; private set; }
        public Guid? PlanId { get; private set; }
        public Guid? EntitlementId { get; private set; }
        public Guid? PaymentOrderId { get; private set; }
        public string? ContentType { get; private set; }
        public Guid? ContentId { get; private set; }
        public string? Source { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public int? LatencyMs { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private EntitlementAuditLog()
        {
        }

        public EntitlementAuditLog(
            Guid? userId,
            string eventType,
            AuditOutcome outcome,
            Guid? planId,
            Guid? entitlementId,
            Guid? paymentOrderId,
            string? contentType,
            Guid? contentId,
            string? source,
            string? errorCode,
            string? traceId,
            string? metadata,
            int? latencyMs = null,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            EventType = eventType;
            Outcome = outcome;
            PlanId = planId;
            EntitlementId = entitlementId;
            PaymentOrderId = paymentOrderId;
            ContentType = contentType;
            ContentId = contentId;
            Source = source;
            ErrorCode = errorCode;
            TraceId = traceId;
            Metadata = metadata;
            LatencyMs = latencyMs;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
