using Yakku.Application.Audit;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Captures audit entries so tests can assert on what was (and was not) recorded.</summary>
internal sealed class RecordingAuditLogWriter : IAuditLogWriter
{
    public List<AuthenticationAuditEntry> Authentication { get; } = [];

    public List<PollAuditEntry> Polls { get; } = [];

    public List<NotificationAuditEntry> Notifications { get; } = [];

    public List<SystemLogEntry> System { get; } = [];

    public Task WriteAuthenticationAsync(AuthenticationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Authentication.Add(entry);
        return Task.CompletedTask;
    }

    public Task WritePollAsync(PollAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Polls.Add(entry);
        return Task.CompletedTask;
    }

    public Task WriteNotificationAsync(NotificationAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Notifications.Add(entry);
        return Task.CompletedTask;
    }

    public Task WriteSystemAsync(SystemLogEntry entry, CancellationToken cancellationToken = default)
    {
        System.Add(entry);
        return Task.CompletedTask;
    }

    public Task<bool> RecordNotificationDeliveryAsync(
        NotificationChannel channel,
        string provider,
        string providerMessageId,
        NotificationDeliveryStatus deliveryStatus,
        DateTime occurredAtUtc,
        string? errorCode = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
