using Yakku.Domain.Enums;

namespace Yakku.Application.Audit
{
    /// <summary>
    /// <c>Subject</c> is the raw login identifier (phone/email). It is hashed before storage, never stored as given.
    /// </summary>
    public sealed record AuthenticationAuditEntry(
        AuditActorType ActorType,
        Guid? ActorId,
        string EventType,
        AuditOutcome Outcome,
        string? FailureReason = null,
        string? Subject = null,
        Guid? SessionId = null,
        IReadOnlyDictionary<string, string?>? Metadata = null);

    /// <summary>
    /// <c>VoterUserId</c> is pseudonymised before storage. Never put the chosen option, custom answer or reason in here.
    /// </summary>
    public sealed record PollAuditEntry(
        Guid? PollId,
        AuditActorType ActorType,
        Guid? ActorUserId,
        string EventType,
        AuditOutcome Outcome,
        string? FromStatus = null,
        string? ToStatus = null,
        string? ErrorCode = null,
        Guid? VoterUserId = null,
        IReadOnlyDictionary<string, string?>? Metadata = null);

    /// <summary>
    /// <c>Recipient</c> is masked before storage. <c>Status</c> records provider acceptance only, not delivery.
    /// </summary>
    public sealed record NotificationAuditEntry(
        NotificationChannel Channel,
        string Provider,
        string? EventType,
        Guid? UserId,
        Guid? DeviceId,
        string? Recipient,
        Guid? CorrelationId,
        int AttemptNumber,
        bool WillRetry,
        NotificationAuditStatus Status,
        string? ProviderMessageId = null,
        string? ErrorCode = null,
        string? ErrorMessage = null,
        int? LatencyMs = null);

    public sealed record SystemLogEntry(
        SystemLogCategory Category,
        SystemLogSeverity Severity,
        string Source,
        string Message,
        string? ExceptionType = null,
        string? HttpMethod = null,
        string? Path = null,
        int? StatusCode = null,
        int? DurationMs = null,
        IReadOnlyDictionary<string, string?>? Metadata = null);
}
