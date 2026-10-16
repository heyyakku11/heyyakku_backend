using Yakku.Domain.Enums;

namespace Yakku.Application.Audit
{
    public sealed class NullAuditLogWriter : IAuditLogWriter
    {
        public static readonly NullAuditLogWriter Instance = new();

        private NullAuditLogWriter()
        {
        }

        public Task WriteAuthenticationAsync(AuthenticationAuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WritePollAsync(PollAuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WriteNotificationAsync(NotificationAuditEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task WriteSystemAsync(SystemLogEntry entry, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<bool> RecordNotificationDeliveryAsync(
            NotificationChannel channel,
            string provider,
            string providerMessageId,
            NotificationDeliveryStatus deliveryStatus,
            DateTime occurredAtUtc,
            string? errorCode = null,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(false);
    }
}
