using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Append-only history. Deliberately has no foreign keys so the trail survives poll and user deletion.
    /// Votes carry a pseudonymous <see cref="VoterRef"/> and never the chosen option, custom answer or reason.
    /// </summary>
    public class PollAuditLog
    {
        public Guid Id { get; private set; }
        public Guid? PollId { get; private set; }
        public AuditActorType ActorType { get; private set; }
        public Guid? ActorUserId { get; private set; }
        public string? VoterRef { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public AuditOutcome Outcome { get; private set; }
        public string? FromStatus { get; private set; }
        public string? ToStatus { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? TraceId { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private PollAuditLog()
        {
        }

        public PollAuditLog(
            Guid? pollId,
            AuditActorType actorType,
            Guid? actorUserId,
            string? voterRef,
            string eventType,
            AuditOutcome outcome,
            string? fromStatus,
            string? toStatus,
            string? errorCode,
            string? traceId,
            string? metadata,
            DateTime? createdAt = null)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            ActorType = actorType;
            ActorUserId = actorUserId;
            VoterRef = voterRef;
            EventType = eventType;
            Outcome = outcome;
            FromStatus = fromStatus;
            ToStatus = toStatus;
            ErrorCode = errorCode;
            TraceId = traceId;
            Metadata = metadata;
            CreatedAt = createdAt ?? DateTime.UtcNow;
        }
    }
}
