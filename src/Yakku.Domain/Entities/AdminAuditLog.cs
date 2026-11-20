using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>Append-only admin action trail. No foreign keys so history survives deletes.</summary>
    public class AdminAuditLog
    {
        public Guid Id { get; private set; }
        public Guid? AdminId { get; private set; }
        public string? ActorRole { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public AuditOutcome Outcome { get; private set; }
        public string? TargetType { get; private set; }
        public Guid? TargetId { get; private set; }
        public string? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public int? LatencyMs { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private AdminAuditLog()
        {
        }

        public AdminAuditLog(
            Guid? adminId,
            string? actorRole,
            string eventType,
            AuditOutcome outcome,
            string? targetType,
            Guid? targetId,
            string? ipAddress,
            string? userAgent,
            string? errorCode,
            string? traceId,
            string? metadata,
            int? latencyMs = null,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            AdminId = adminId;
            ActorRole = actorRole;
            EventType = eventType;
            Outcome = outcome;
            TargetType = targetType;
            TargetId = targetId;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            ErrorCode = errorCode;
            TraceId = traceId;
            Metadata = metadata;
            LatencyMs = latencyMs;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
