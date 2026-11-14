using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Application.PushNotifications.DTOs;
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
    /// Sends the vote pushes that go out on their own: a card's first votes and the votes of cards in individual
    /// mode. Every row is claimed with a conditional update (compare-and-set on its status and lease), so several
    /// instances can run it against the same table and a row claimed by a crashed instance becomes claimable again
    /// when its lease runs out. Delivery is at least once: a crash between the provider accepting the push and the
    /// row being marked submitted sends it again, with the same <c>deliveryId</c> in the payload.
    /// </summary>
    public sealed class VoteNotificationDispatcher
    {
        private readonly YakkuDbContext _db;
        private readonly VotePushSender _sender;
        private readonly VoteNotificationBatchingOptions _options;
        private readonly VoteNotificationMetrics _metrics;
        private readonly TimeProvider _time;
        private readonly ILogger<VoteNotificationDispatcher> _logger;

        public VoteNotificationDispatcher(
            YakkuDbContext db,
            VotePushSender sender,
            VoteNotificationBatchingOptions options,
            VoteNotificationMetrics metrics,
            TimeProvider time,
            ILogger<VoteNotificationDispatcher> logger)
        {
            _db = db;
            _sender = sender;
            _options = options;
            _metrics = metrics;
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
                .Take(_options.SchedulerBatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var id in ids)
            {
                var token = Guid.NewGuid();
                var leaseUntil = now + TimeSpan.FromSeconds(_options.SendLeaseSeconds);
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
                    _metrics.ClaimConflicts.Add(1);
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

            var send = new PushSendResult(PushOutcome.Skipped, null);
            if (notification is not null)
            {
                // The event id is the delivery id: it stays the same on every retry of this push.
                send = await _sender.SendAsync(
                    ev.OwnerId,
                    PushPayloads.ForNotification(notification, ev.Id),
                    cancellationToken);
            }

            var now = Now();
            var owned = _db.VoteNotificationEvents.Where(e => e.Id == id && e.LeaseToken == token);
            switch (send.Outcome)
            {
                case PushOutcome.Submitted:
                    var messageId = send.MessageId;
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Submitted)
                            .SetProperty(e => e.SubmittedAt, (DateTime?)now)
                            .SetProperty(e => e.ProviderMessageId, messageId)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    _metrics.PushSent.Add(1, new KeyValuePair<string, object?>("kind", "individual"));
                    _metrics.DeliveryLatencyMs.Record(
                        Math.Max(0, (now - ev.CreatedAt).TotalMilliseconds),
                        new KeyValuePair<string, object?>("kind", "individual"));
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
                    if (ev.Attempts >= _options.MaxAttempts)
                    {
                        _logger.LogWarning("Vote push for event {EventId} failed permanently.", id);
                        await owned.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(e => e.Status, VoteNotificationStatus.Failed)
                                .SetProperty(e => e.LeaseToken, (Guid?)null)
                                .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        _metrics.PushFailures.Add(
                            1,
                            new KeyValuePair<string, object?>("kind", "individual"),
                            new KeyValuePair<string, object?>("final", true));
                        break;
                    }

                    var retryAt = now + _options.RetryDelay(ev.Attempts);
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, VoteNotificationStatus.Pending)
                            .SetProperty(e => e.NextAttemptAt, (DateTime?)retryAt)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    _metrics.PushFailures.Add(
                        1,
                        new KeyValuePair<string, object?>("kind", "individual"),
                        new KeyValuePair<string, object?>("final", false));
                    _metrics.Retries.Add(1, new KeyValuePair<string, object?>("kind", "individual"));
                    break;
            }
        }
    }
}
