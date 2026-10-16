using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.Audit;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Configuration;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Audit
{
    public sealed record AuditRetentionResult(
        int AuthenticationDeleted,
        int NotificationDeleted,
        int SystemDeleted,
        int PollDeleted);

    /// <summary>
    /// Enforces the retention policy: rows older than the configured window are removed in small batches so a
    /// large backlog never holds a long lock.
    /// </summary>
    public sealed class AuditRetentionService
    {
        private const int BatchSize = 500;
        private const int MaxBatchesPerTable = 100;

        private readonly YakkuDbContext _context;
        private readonly AuditRetentionOptions _options;
        private readonly IAuditLogWriter _audit;

        public AuditRetentionService(
            YakkuDbContext context,
            AuditRetentionOptions options,
            IAuditLogWriter audit)
        {
            _context = context;
            _options = options;
            _audit = audit;
        }

        public async Task<AuditRetentionResult> PurgeAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            var authCutoff = utcNow.AddDays(-_options.AuthenticationDays);
            var authentication = await InBatchesAsync(async token =>
            {
                var ids = await _context.AuthenticationAuditLogs
                    .Where(log => log.CreatedAt < authCutoff)
                    .OrderBy(log => log.CreatedAt)
                    .Select(log => log.Id)
                    .Take(BatchSize)
                    .ToListAsync(token);
                return ids.Count == 0
                    ? 0
                    : await _context.AuthenticationAuditLogs
                        .Where(log => ids.Contains(log.Id))
                        .ExecuteDeleteAsync(token);
            }, cancellationToken);

            var notificationCutoff = utcNow.AddDays(-_options.NotificationDays);
            var notification = await InBatchesAsync(async token =>
            {
                var ids = await _context.NotificationAuditLogs
                    .Where(log => log.CreatedAt < notificationCutoff)
                    .OrderBy(log => log.CreatedAt)
                    .Select(log => log.Id)
                    .Take(BatchSize)
                    .ToListAsync(token);
                return ids.Count == 0
                    ? 0
                    : await _context.NotificationAuditLogs
                        .Where(log => ids.Contains(log.Id))
                        .ExecuteDeleteAsync(token);
            }, cancellationToken);

            var pollCutoff = utcNow.AddDays(-_options.PollDays);
            var poll = await InBatchesAsync(async token =>
            {
                var ids = await _context.PollAuditLogs
                    .Where(log => log.CreatedAt < pollCutoff)
                    .OrderBy(log => log.CreatedAt)
                    .Select(log => log.Id)
                    .Take(BatchSize)
                    .ToListAsync(token);
                return ids.Count == 0
                    ? 0
                    : await _context.PollAuditLogs
                        .Where(log => ids.Contains(log.Id))
                        .ExecuteDeleteAsync(token);
            }, cancellationToken);

            var systemCutoff = utcNow.AddDays(-_options.SystemDays);
            var system = await InBatchesAsync(async token =>
            {
                var ids = await _context.SystemLogs
                    .Where(log => log.CreatedAt < systemCutoff)
                    .OrderBy(log => log.CreatedAt)
                    .Select(log => log.Id)
                    .Take(BatchSize)
                    .ToListAsync(token);
                return ids.Count == 0
                    ? 0
                    : await _context.SystemLogs
                        .Where(log => ids.Contains(log.Id))
                        .ExecuteDeleteAsync(token);
            }, cancellationToken);

            stopwatch.Stop();
            var result = new AuditRetentionResult(authentication, notification, system, poll);

            // Written after the sweep so this run's own summary is never swept up.
            await _audit.WriteSystemAsync(
                new SystemLogEntry(
                    SystemLogCategory.BackgroundJob,
                    SystemLogSeverity.Information,
                    nameof(AuditRetentionService),
                    "Audit retention finished.",
                    DurationMs: (int)stopwatch.ElapsedMilliseconds,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["job"] = AuditEvents.Jobs.AuditRetention,
                        ["authenticationDeleted"] = authentication.ToString(),
                        ["notificationDeleted"] = notification.ToString(),
                        ["pollDeleted"] = poll.ToString(),
                        ["systemDeleted"] = system.ToString()
                    }),
                CancellationToken.None);
            return result;
        }

        private static async Task<int> InBatchesAsync(
            Func<CancellationToken, Task<int>> deleteBatch,
            CancellationToken cancellationToken)
        {
            var total = 0;
            for (var batch = 0; batch < MaxBatchesPerTable; batch++)
            {
                var deleted = await deleteBatch(cancellationToken);
                total += deleted;
                if (deleted < BatchSize)
                {
                    break;
                }
            }

            return total;
        }
    }
}
