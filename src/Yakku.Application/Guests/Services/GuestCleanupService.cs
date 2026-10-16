using System.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Guests.Services
{
    public sealed class GuestCleanupService : IGuestCleanupService
    {
        private static readonly TimeSpan SlowRunThreshold = TimeSpan.FromSeconds(60);

        private readonly IGuestRepository _guests;
        private readonly IMediaUploadSessionRepository _sessions;
        private readonly IMediaUploadCleanupService _mediaCleanup;
        private readonly GuestCleanupOptions _options;
        private readonly IAuditLogWriter _audit;

        public GuestCleanupService(
            IGuestRepository guests,
            IMediaUploadSessionRepository sessions,
            IMediaUploadCleanupService mediaCleanup,
            GuestCleanupOptions options,
            IAuditLogWriter audit)
        {
            _guests = guests;
            _sessions = sessions;
            _mediaCleanup = mediaCleanup;
            _options = options;
            _audit = audit;
        }

        public async Task<GuestCleanupResult> CleanupAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var utcNow = DateTime.UtcNow;
            var cutoff = utcNow - _options.GracePeriod;

            var ids = await _guests.ListExpiredIdsAsync(cutoff, _options.BatchSize, cancellationToken);
            if (ids.Count == 0)
            {
                return new GuestCleanupResult(0, 0, 0, 0, 0);
            }

            var invalidated = 0;
            var failed = 0;
            var live = new List<Guid>(ids.Count);

            // 1. Invalidate dependent sessions so they can no longer be used and become eligible for purging.
            foreach (var id in ids)
            {
                try
                {
                    await _guests.ExecuteInTransactionAsync(async transactionToken =>
                    {
                        invalidated += await _sessions.ExpireGuestSessionsAsync(id, utcNow, transactionToken);
                    }, cancellationToken);
                    live.Add(id);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                }
            }

            // 2. Purge the stored objects of those sessions.
            try
            {
                await _mediaCleanup.CleanupAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
            }

            // 3. Delete session rows and the guest, honouring the Restrict foreign key: a guest is only removed
            //    once no session row references it.
            var deleted = 0;
            var deferred = 0;
            foreach (var id in live)
            {
                try
                {
                    var removed = false;
                    var remaining = 0;
                    await _guests.ExecuteInTransactionAsync(async transactionToken =>
                    {
                        await _sessions.DeletePurgedGuestSessionsAsync(id, transactionToken);
                        remaining = await _sessions.CountGuestSessionsAsync(id, transactionToken);
                        if (remaining == 0)
                        {
                            removed = await _guests.DeleteExpiredByIdAsync(id, cutoff, transactionToken);
                        }
                    }, cancellationToken);

                    if (removed)
                    {
                        deleted++;
                    }
                    else if (remaining > 0)
                    {
                        deferred++;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                }
            }

            stopwatch.Stop();
            var result = new GuestCleanupResult(ids.Count, invalidated, deleted, deferred, failed);
            await WriteSummaryAsync(result, stopwatch.Elapsed);
            return result;
        }

        private async Task WriteSummaryAsync(GuestCleanupResult result, TimeSpan elapsed)
        {
            var metadata = new Dictionary<string, string?>
            {
                ["job"] = AuditEvents.Jobs.GuestCleanup,
                ["examined"] = result.Examined.ToString(),
                ["sessionsInvalidated"] = result.SessionsInvalidated.ToString(),
                ["guestsDeleted"] = result.GuestsDeleted.ToString(),
                ["guestsDeferred"] = result.GuestsDeferred.ToString(),
                ["guestsFailed"] = result.GuestsFailed.ToString()
            };

            await _audit.WriteSystemAsync(
                new SystemLogEntry(
                    SystemLogCategory.BackgroundJob,
                    result.GuestsFailed > 0 ? SystemLogSeverity.Warning : SystemLogSeverity.Information,
                    nameof(GuestCleanupService),
                    result.GuestsFailed > 0 ? "Guest cleanup finished with failures." : "Guest cleanup finished.",
                    DurationMs: (int)elapsed.TotalMilliseconds,
                    Metadata: metadata),
                CancellationToken.None);

            if (elapsed > SlowRunThreshold)
            {
                await _audit.WriteSystemAsync(
                    new SystemLogEntry(
                        SystemLogCategory.Performance,
                        SystemLogSeverity.Warning,
                        nameof(GuestCleanupService),
                        "Guest cleanup run was slow.",
                        DurationMs: (int)elapsed.TotalMilliseconds,
                        Metadata: metadata),
                    CancellationToken.None);
            }
        }
    }
}
