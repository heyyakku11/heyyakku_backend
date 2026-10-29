using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Push
{
    public enum PushOutcome
    {
        /// <summary>At least one device accepted the push.</summary>
        Submitted,

        /// <summary>Nothing reached the asker but a retry may help.</summary>
        Retry,

        /// <summary>Nothing to send to, or nothing a retry can fix.</summary>
        Skipped
    }

    /// <summary>
    /// Sends the outbox: the immediate vote pushes and the digest windows. Every row is claimed with a conditional
    /// update (compare-and-set on its status and lease), so several instances can run it against the same tables
    /// and a row claimed by a crashed instance becomes claimable again when its lease runs out.
    /// </summary>
    public sealed class VoteNotificationDispatcher
    {
        private readonly YakkuDbContext _db;
        private readonly IPushNotificationService _push;
        private readonly TimeProvider _time;
        private readonly ILogger<VoteNotificationDispatcher> _logger;

        public VoteNotificationDispatcher(
            YakkuDbContext db,
            IPushNotificationService push,
            TimeProvider time,
            ILogger<VoteNotificationDispatcher> logger)
        {
            _db = db;
            _push = push;
            _time = time;
            _logger = logger;
        }

        /// <summary>
        /// Retry only when nothing reached the asker. After a partial success a retry would send a second push to
        /// the devices that already got one.
        /// </summary>
        public static PushOutcome Classify(PushUserNotificationResult? result, Exception? exception)
        {
            if (exception is AppException { StatusCode: 400 or 404 })
            {
                return PushOutcome.Skipped;
            }

            if (exception is not null)
            {
                return PushOutcome.Retry;
            }

            if (result is null)
            {
                return PushOutcome.Skipped;
            }

            if (result.Successful > 0)
            {
                return PushOutcome.Submitted;
            }

            return result.RetryableFailures > 0 ? PushOutcome.Retry : PushOutcome.Skipped;
        }

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        public async Task<int> ProcessImmediateAsync(CancellationToken cancellationToken)
        {
            var now = Now();
            Expression<Func<VoteNotificationEvent, bool>> claimable = e =>
                e.Kind == VoteNotificationKind.Immediate
                && ((e.Status == VoteNotificationStatus.Pending && e.NextAttemptAt <= now)
                    || (e.Status == VoteNotificationStatus.Sending && e.LeaseUntil < now));

            var ids = await _db.VoteNotificationEvents
                .AsNoTracking()
                .Where(claimable)
                .OrderBy(e => e.NextAttemptAt)
                .Select(e => e.Id)
                .Take(VoteNotificationOptions.BatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var id in ids)
            {
                var token = Guid.NewGuid();
                var leaseUntil = now + VoteNotificationOptions.Lease;
                var claimed = await _db.VoteNotificationEvents
                    .Where(claimable)
                    .Where(e => e.Id == id)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Sending)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)leaseUntil)
                            .SetProperty(e => e.LeaseToken, (Guid?)token)
                            .SetProperty(e => e.Attempts, e => e.Attempts + 1),
                        cancellationToken);
                if (claimed == 0)
                {
                    // Another instance got it first.
                    continue;
                }

                handled++;
                await SendImmediateAsync(id, token, cancellationToken);
            }

            return handled;
        }

        private async Task SendImmediateAsync(Guid id, Guid token, CancellationToken cancellationToken)
        {
            var ev = await _db.VoteNotificationEvents.AsNoTracking().FirstAsync(e => e.Id == id, cancellationToken);
            var notification = await _db.Notifications
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == ev.NotificationId, cancellationToken);

            PushOutcome outcome;
            if (notification is null)
            {
                outcome = PushOutcome.Skipped;
            }
            else
            {
                outcome = await SendAsync(ev.OwnerId, PushPayloads.ForNotification(notification), cancellationToken);
            }

            var now = Now();
            var owned = _db.VoteNotificationEvents.Where(e => e.Id == id && e.LeaseToken == token);
            switch (outcome)
            {
                case PushOutcome.Submitted:
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Submitted)
                            .SetProperty(e => e.SubmittedAt, (DateTime?)now)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    break;
                case PushOutcome.Skipped:
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Skipped)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    break;
                default:
                    if (ev.Attempts >= PushQueueOptions.MaxAttempts)
                    {
                        _logger.LogWarning("Vote push for event {EventId} failed permanently.", id);
                        await owned.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(e => e.Status, VoteNotificationStatus.Failed)
                                .SetProperty(e => e.LeaseToken, (Guid?)null)
                                .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        break;
                    }

                    var retryAt = now + PushQueueOptions.BackoffWithJitter(ev.Attempts, Random.Shared.NextDouble());
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Pending)
                            .SetProperty(e => e.NextAttemptAt, (DateTime?)retryAt)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    break;
            }
        }

        public async Task<int> ProcessWindowsAsync(CancellationToken cancellationToken)
        {
            var now = Now();
            Expression<Func<VoteDigestWindow, bool>> claimable = w =>
                ((w.Status == VoteDigestWindowStatus.Open && w.EndsAt <= now)
                 || w.Status == VoteDigestWindowStatus.Due
                 || (w.Status == VoteDigestWindowStatus.Processing && w.LeaseUntil < now))
                && (w.NextAttemptAt == null || w.NextAttemptAt <= now);

            var ids = await _db.VoteDigestWindows
                .AsNoTracking()
                .Where(claimable)
                .OrderBy(w => w.EndsAt)
                .Select(w => w.Id)
                .Take(VoteNotificationOptions.BatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var id in ids)
            {
                var token = Guid.NewGuid();
                var leaseUntil = now + VoteNotificationOptions.Lease;
                var claimed = await _db.VoteDigestWindows
                    .Where(claimable)
                    .Where(w => w.Id == id)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(w => w.Status, VoteDigestWindowStatus.Processing)
                            .SetProperty(w => w.LeaseUntil, (DateTime?)leaseUntil)
                            .SetProperty(w => w.LeaseToken, (Guid?)token)
                            .SetProperty(w => w.Attempts, w => w.Attempts + 1),
                        cancellationToken);
                if (claimed == 0)
                {
                    continue;
                }

                handled++;
                await SendDigestAsync(id, token, cancellationToken);
            }

            return handled;
        }

        private async Task SendDigestAsync(Guid windowId, Guid token, CancellationToken cancellationToken)
        {
            var window = await _db.VoteDigestWindows.AsNoTracking().FirstAsync(w => w.Id == windowId, cancellationToken);
            var endsAt = window.EndsAt;

            // Eligible: not yet submitted, and not read before the window's cutoff. A read after the cutoff does not
            // remove an event, so the count is the same whenever the worker gets to the window.
            Expression<Func<VoteNotificationEvent, bool>> eligible = e =>
                e.WindowId == windowId
                && e.Status == VoteNotificationStatus.Pending
                && _db.Notifications.Any(n => n.Id == e.NotificationId && (!n.IsRead || n.ReadAt > endsAt));

            var count = await _db.VoteNotificationEvents.CountAsync(eligible, cancellationToken);

            var outcome = PushOutcome.Skipped;
            if (count > 0)
            {
                var contentType = window.ContentType == ShareContentType.Poll ? "poll" : "check";
                outcome = await SendAsync(
                    window.OwnerId,
                    PushPayloads.ForDigest(contentType, window.ContentId, count),
                    cancellationToken);
            }

            var now = Now();
            var ownedWindow = _db.VoteDigestWindows.Where(w => w.Id == windowId && w.LeaseToken == token);
            switch (outcome)
            {
                case PushOutcome.Submitted:
                    await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        var updated = await ownedWindow.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(w => w.Status, VoteDigestWindowStatus.Sent)
                                .SetProperty(w => w.EventCount, count)
                                .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                .SetProperty(w => w.LeaseToken, (Guid?)null)
                                .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        if (updated == 0)
                        {
                            // The lease ran out and another instance took the window over: leave it to them.
                            await transaction.RollbackAsync(cancellationToken);
                            return;
                        }

                        await _db.VoteNotificationEvents.Where(eligible).ExecuteUpdateAsync(
                            set => set
                                .SetProperty(e => e.Status, VoteNotificationStatus.Submitted)
                                .SetProperty(e => e.SubmittedAt, (DateTime?)now),
                            cancellationToken);
                        await SkipRemainingAsync(windowId, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }

                    break;
                case PushOutcome.Skipped:
                    await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        var updated = await ownedWindow.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(w => w.Status, VoteDigestWindowStatus.Skipped)
                                .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                .SetProperty(w => w.LeaseToken, (Guid?)null)
                                .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        if (updated == 0)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return;
                        }

                        await SkipRemainingAsync(windowId, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }

                    break;
                default:
                    if (window.Attempts >= PushQueueOptions.MaxAttempts)
                    {
                        _logger.LogWarning("Digest push for window {WindowId} failed permanently.", windowId);
                        await ownedWindow.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(w => w.Status, VoteDigestWindowStatus.Failed)
                                .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                .SetProperty(w => w.LeaseToken, (Guid?)null)
                                .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        break;
                    }

                    var retryAt = now + PushQueueOptions.BackoffWithJitter(window.Attempts, Random.Shared.NextDouble());
                    await ownedWindow.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(w => w.Status, VoteDigestWindowStatus.Due)
                            .SetProperty(w => w.NextAttemptAt, (DateTime?)retryAt)
                            .SetProperty(w => w.LeaseToken, (Guid?)null)
                            .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    break;
            }
        }

        /// <summary>Events of the window that were read before the cutoff (or are otherwise left over) never get a push.</summary>
        private Task<int> SkipRemainingAsync(Guid windowId, CancellationToken cancellationToken)
        {
            return _db.VoteNotificationEvents
                .Where(e => e.WindowId == windowId && e.Status == VoteNotificationStatus.Pending)
                .ExecuteUpdateAsync(
                    set => set.SetProperty(e => e.Status, VoteNotificationStatus.Skipped),
                    cancellationToken);
        }

        private async Task<PushOutcome> SendAsync(
            Guid ownerId,
            NotificationPayload payload,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _push.SendToUserAsync(ownerId, payload, cancellationToken);
                return Classify(result, null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var outcome = Classify(null, exception);
                if (outcome == PushOutcome.Retry)
                {
                    _logger.LogWarning(exception, "Vote push failed for owner {OwnerId}.", ownerId);
                }
                else
                {
                    _logger.LogInformation("No push-eligible device for owner {OwnerId}; skipping vote push.", ownerId);
                }

                return outcome;
            }
        }
    }
}
