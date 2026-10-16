using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Audit
{
    /// <summary>
    /// Writes audit and system logs on a dedicated DbContext so they commit independently of the request's own
    /// transaction (a rolled-back operation still leaves its failure trail) and never throw into the caller.
    /// </summary>
    public sealed class AuditLogWriter : IAuditLogWriter
    {
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(3);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogWriter> _logger;

        public AuditLogWriter(
            IServiceScopeFactory scopeFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<AuditLogWriter> logger)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Task WriteAuthenticationAsync(
            AuthenticationAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            var request = CaptureRequest();
            var log = new AuthenticationAuditLog(
                entry.ActorType,
                entry.ActorId,
                AuditSanitizer.HashSubject(entry.Subject),
                AuditSanitizer.Truncate(entry.EventType, 64)!,
                entry.Outcome,
                AuditSanitizer.SanitizeText(entry.FailureReason, 128),
                entry.SessionId,
                request.IpAddress,
                request.UserAgent,
                request.TraceId,
                AuditSanitizer.SerializeMetadata(entry.Metadata));
            return SaveAsync(context => context.AuthenticationAuditLogs.Add(log), "authentication");
        }

        public Task WritePollAsync(PollAuditEntry entry, CancellationToken cancellationToken = default)
        {
            var request = CaptureRequest();
            var voterRef = entry.VoterUserId is Guid voter
                ? AuditSanitizer.PseudonymizeVoter(voter, entry.PollId)
                : null;
            var log = new PollAuditLog(
                entry.PollId,
                entry.ActorType,
                voterRef is null ? entry.ActorUserId : null,
                voterRef,
                AuditSanitizer.Truncate(entry.EventType, 64)!,
                entry.Outcome,
                AuditSanitizer.Truncate(entry.FromStatus, 20),
                AuditSanitizer.Truncate(entry.ToStatus, 20),
                AuditSanitizer.SanitizeText(entry.ErrorCode, 64),
                request.TraceId,
                AuditSanitizer.SerializeMetadata(entry.Metadata));
            return SaveAsync(context => context.PollAuditLogs.Add(log), "poll");
        }

        public Task WriteNotificationAsync(
            NotificationAuditEntry entry,
            CancellationToken cancellationToken = default)
        {
            var log = new NotificationAuditLog(
                entry.Channel,
                AuditSanitizer.Truncate(entry.Provider, 32)!,
                AuditSanitizer.Truncate(entry.EventType, 64),
                entry.UserId,
                entry.DeviceId,
                AuditSanitizer.MaskRecipient(entry.Recipient),
                entry.CorrelationId,
                Math.Max(1, entry.AttemptNumber),
                entry.WillRetry,
                entry.Status,
                AuditSanitizer.Truncate(entry.ProviderMessageId, 256),
                AuditSanitizer.SanitizeText(entry.ErrorCode, 64),
                AuditSanitizer.SanitizeText(entry.ErrorMessage, 500),
                entry.LatencyMs is int latency ? Math.Max(0, latency) : null);
            return SaveAsync(context => context.NotificationAuditLogs.Add(log), "notification");
        }

        public Task WriteSystemAsync(SystemLogEntry entry, CancellationToken cancellationToken = default)
        {
            var request = CaptureRequest();
            var log = new SystemLog(
                entry.Category,
                entry.Severity,
                AuditSanitizer.Truncate(entry.Source, 100)!,
                AuditSanitizer.SanitizeText(entry.Message, 500) ?? "(no message)",
                AuditSanitizer.Truncate(entry.ExceptionType, 200),
                AuditSanitizer.Truncate(entry.HttpMethod, 10),
                AuditSanitizer.NormalizePath(entry.Path),
                entry.StatusCode is >= 100 and <= 599 ? entry.StatusCode : null,
                entry.DurationMs is int duration ? Math.Max(0, duration) : null,
                request.TraceId,
                AuditSanitizer.SerializeMetadata(entry.Metadata));
            return SaveAsync(context => context.SystemLogs.Add(log), "system");
        }

        public async Task<bool> RecordNotificationDeliveryAsync(
            NotificationChannel channel,
            string provider,
            string providerMessageId,
            NotificationDeliveryStatus deliveryStatus,
            DateTime occurredAtUtc,
            string? errorCode = null,
            string? errorMessage = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(providerMessageId))
            {
                return false;
            }

            try
            {
                using var timeout = new CancellationTokenSource(WriteTimeout);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<YakkuDbContext>();
                var attempt = await context.NotificationAuditLogs
                    .Where(log => log.Channel == channel
                        && log.Provider == provider
                        && log.ProviderMessageId == providerMessageId
                        && log.Status == NotificationAuditStatus.Accepted)
                    .OrderByDescending(log => log.CreatedAt)
                    .FirstOrDefaultAsync(timeout.Token);
                if (attempt is null)
                {
                    return false;
                }

                var applied = attempt.ApplyDeliveryReport(
                    deliveryStatus,
                    occurredAtUtc,
                    AuditSanitizer.SanitizeText(errorCode, 64),
                    AuditSanitizer.SanitizeText(errorMessage, 500));
                if (applied)
                {
                    await context.SaveChangesAsync(timeout.Token);
                }

                return applied;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Notification delivery report could not be recorded ({ExceptionType}).",
                    exception.GetType().Name);
                return false;
            }
        }

        private async Task SaveAsync(Action<YakkuDbContext> add, string kind)
        {
            try
            {
                using var timeout = new CancellationTokenSource(WriteTimeout);
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<YakkuDbContext>();
                add(context);
                await context.SaveChangesAsync(timeout.Token);
            }
            catch (Exception exception)
            {
                // Auditing is best effort: log the type only, never the entry or the exception message.
                _logger.LogWarning(
                    "Failed to write {AuditKind} audit log ({ExceptionType}).",
                    kind,
                    exception.GetType().Name);
            }
        }

        private RequestInfo CaptureRequest()
        {
            var http = _httpContextAccessor.HttpContext;
            var traceId = AuditSanitizer.Truncate(
                Activity.Current?.TraceId.ToString() ?? http?.TraceIdentifier,
                64);
            if (http is null)
            {
                return new RequestInfo(null, null, traceId);
            }

            var ip = http.Connection.RemoteIpAddress;
            if (ip is not null && ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            var userAgent = http.Request.Headers.UserAgent.ToString();
            return new RequestInfo(ip, AuditSanitizer.SanitizeUserAgent(userAgent), traceId);
        }

        private readonly record struct RequestInfo(IPAddress? IpAddress, string? UserAgent, string? TraceId);
    }
}
