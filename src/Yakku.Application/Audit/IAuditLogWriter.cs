using Yakku.Domain.Enums;

namespace Yakku.Application.Audit
{
    /// <summary>
    /// Best-effort audit sink. Implementations must never throw: a failed audit write may not fail the
    /// operation being audited. Sanitisation (hashing, masking, redaction) is the implementation's job.
    /// </summary>
    public interface IAuditLogWriter
    {
        Task WriteAuthenticationAsync(AuthenticationAuditEntry entry, CancellationToken cancellationToken = default);

        Task WritePollAsync(PollAuditEntry entry, CancellationToken cancellationToken = default);

        Task WriteNotificationAsync(NotificationAuditEntry entry, CancellationToken cancellationToken = default);

        Task WriteSystemAsync(SystemLogEntry entry, CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the provider's delivery report for an attempt that was previously accepted.
        /// Returns false when no matching accepted attempt exists.
        /// </summary>
        Task<bool> RecordNotificationDeliveryAsync(
            NotificationChannel channel,
            string provider,
            string providerMessageId,
            NotificationDeliveryStatus deliveryStatus,
            DateTime occurredAtUtc,
            string? errorCode = null,
            string? errorMessage = null,
            CancellationToken cancellationToken = default);
    }
}
