using System.Net;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class AuthenticationAuditLog
    {
        public Guid Id { get; private set; }
        public AuditActorType ActorType { get; private set; }
        public Guid? ActorId { get; private set; }
        public string? SubjectHash { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public AuditOutcome Outcome { get; private set; }
        public string? FailureReason { get; private set; }
        public Guid? SessionId { get; private set; }
        public IPAddress? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public int? LatencyMs { get; private set; }
        /// <summary>Normalized request path when this row was written from an API call; null for non-HTTP work.</summary>
        public string? ApiRoute { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private AuthenticationAuditLog()
        {
        }

        public AuthenticationAuditLog(
            AuditActorType actorType,
            Guid? actorId,
            string? subjectHash,
            string eventType,
            AuditOutcome outcome,
            string? failureReason,
            Guid? sessionId,
            IPAddress? ipAddress,
            string? userAgent,
            string? traceId,
            string? metadata,
            int? latencyMs = null,
            string? apiRoute = null,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            ActorType = actorType;
            ActorId = actorId;
            SubjectHash = subjectHash;
            EventType = eventType;
            Outcome = outcome;
            FailureReason = failureReason;
            SessionId = sessionId;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            TraceId = traceId;
            Metadata = metadata;
            LatencyMs = latencyMs;
            ApiRoute = apiRoute;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
