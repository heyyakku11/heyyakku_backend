using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>Append-only trail for checks, fun cards, and personality (not polls). No FKs.</summary>
    public class ContentAuditLog
    {
        public Guid Id { get; private set; }
        public string ContentType { get; private set; } = string.Empty;
        public Guid? ContentId { get; private set; }
        public AuditActorType ActorType { get; private set; }
        public Guid? ActorId { get; private set; }
        public string? VoterRef { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public AuditOutcome Outcome { get; private set; }
        public string? FromStatus { get; private set; }
        public string? ToStatus { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public int? LatencyMs { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private ContentAuditLog()
        {
        }

        public ContentAuditLog(
            string contentType,
            Guid? contentId,
            AuditActorType actorType,
            Guid? actorId,
            string? voterRef,
            string eventType,
            AuditOutcome outcome,
            string? fromStatus,
            string? toStatus,
            string? errorCode,
            string? traceId,
            string? metadata,
            int? latencyMs = null,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            ContentType = contentType;
            ContentId = contentId;
            ActorType = actorType;
            ActorId = actorId;
            VoterRef = voterRef;
            EventType = eventType;
            Outcome = outcome;
            FromStatus = fromStatus;
            ToStatus = toStatus;
            ErrorCode = errorCode;
            TraceId = traceId;
            Metadata = metadata;
            LatencyMs = latencyMs;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
