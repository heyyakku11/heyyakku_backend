using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Redis;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Sends the aggregated pushes. A batch is taken from two places: the Redis schedule, which hands out a batch
    /// as soon as it is due, and a database sweep, which takes whatever Redis could not (retries, a worker that died
    /// mid-send, an open batch Redis forgot). Either way the batch is claimed in the database first with a
    /// compare-and-set on its status and lease token, so only one worker sends it. Order of a flush: claim, close the
    /// batch in Redis (later votes start a new batch), count the votes still eligible, send, then mark it sent. A
    /// batch is only marked sent after the push provider accepted the push. A crash between accepting and marking
    /// sends the push again, with the same <c>deliveryId</c> (the batch id); delivery is at least once.
    /// </summary>
    public sealed class VoteBatchDispatcher
    {
        private readonly YakkuDbContext _db;
        private readonly IVoteBatchingStore _store;
        private readonly VotePushSender _sender;
        private readonly CardModeEvaluator _evaluator;
        private readonly VoteNotificationBatchingOptions _options;
        private readonly VoteNotificationMetrics _metrics;
        private readonly TimeProvider _time;
        private readonly ILogger<VoteBatchDispatcher> _logger;

        public VoteBatchDispatcher(
            YakkuDbContext db,
            IVoteBatchingStore store,
            VotePushSender sender,
            CardModeEvaluator evaluator,
            VoteNotificationBatchingOptions options,
            VoteNotificationMetrics metrics,
            TimeProvider time,
            ILogger<VoteBatchDispatcher> logger)
        {
            _db = db;
            _store = store;
            _sender = sender;
            _evaluator = evaluator;
            _options = options;
            _metrics = metrics;
            _time = time;
            _logger = logger;
        }

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        /// <summary>One poll of the Redis schedule: sends the batches that are due and runs the due evaluations.</summary>
        public async Task<(int Handled, long QueueSize)> ProcessScheduleAsync(CancellationToken cancellationToken)
        {
            DueWork work;
            try
            {
                work = await _store.PollDueAsync(
                    _options.SchedulerBatchSize,
                    _options.SchedulerBatchSize,
                    TimeSpan.FromSeconds(_options.QueueLeaseSeconds),
                    TimeSpan.FromSeconds(_options.QueueLeaseSeconds),
                    cancellationToken);
            }
            catch (VoteBatchingStoreException exception)
            {
                // The database sweep keeps batches moving without Redis.
                _metrics.RedisFailures.Add(1);
                _logger.LogWarning(exception, "Could not poll the vote batch schedule.");
                return (0, 0);
            }

            _metrics.SetQueue(work.QueueSize, work.DueRemaining);

            var handled = 0;
            foreach (var due in work.Batches)
            {
                if (await FlushScheduledAsync(due, work.Now, cancellationToken))
                {
                    handled++;
                }
            }

            foreach (var cardKey in work.Evaluations)
            {
                await _evaluator.EvaluateAsync(cardKey, cancellationToken);
                handled++;
            }

            return (handled, work.QueueSize);
        }

        /// <summary>Claims and sends the batches the database knows are due, whatever Redis says.</summary>
        public async Task<int> SweepAsync(CancellationToken cancellationToken)
        {
            var now = Now();
            var token = Guid.NewGuid();
            var leaseUntil = now + TimeSpan.FromSeconds(_options.SendLeaseSeconds);
            var overdue = now - TimeSpan.FromSeconds(_options.OverdueGraceSeconds);
            var limit = _options.SchedulerBatchSize;

            // An open batch is only taken after a grace period past its due time, so the sweep does not race the
            // Redis schedule that normally sends it.
            var ids = await _db.Database
                .SqlQuery<Guid>($@"UPDATE vote_digest_windows
                                   SET status = 'PROCESSING', lease_until = {leaseUntil}, lease_token = {token},
                                       claimed_at = {now}, attempts = attempts + 1
                                   WHERE id IN (
                                       SELECT id FROM vote_digest_windows
                                       WHERE (status = 'OPEN' AND ends_at <= {overdue})
                                          OR (status = 'DUE' AND (next_attempt_at IS NULL OR next_attempt_at <= {now}))
                                          OR (status = 'PROCESSING' AND lease_until < {now})
                                       ORDER BY ends_at
                                       LIMIT {limit}
                                       FOR UPDATE SKIP LOCKED)
                                   RETURNING id AS ""Value""")
                .ToListAsync(cancellationToken);

            foreach (var id in ids)
            {
                await FlushClaimedAsync(id, token, now, cancellationToken);
            }

            return ids.Count;
        }

        private async Task<bool> FlushScheduledAsync(DueBatch due, long redisNow, CancellationToken cancellationToken)
        {
            var now = Now();
            var token = Guid.NewGuid();
            var leaseUntil = now + TimeSpan.FromSeconds(_options.SendLeaseSeconds);
            var id = due.BatchId;

            Expression<Func<VoteDigestWindow, bool>> claimable = w =>
                w.Status == VoteDigestWindowStatus.Open
                || (w.Status == VoteDigestWindowStatus.Due && (w.NextAttemptAt == null || w.NextAttemptAt <= now))
                || (w.Status == VoteDigestWindowStatus.Processing && w.LeaseUntil < now);

            var claimed = await _db.VoteDigestWindows
                .Where(w => w.Id == id)
                .Where(claimable)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(w => w.Status, VoteDigestWindowStatus.Processing)
                        .SetProperty(w => w.LeaseUntil, (DateTime?)leaseUntil)
                        .SetProperty(w => w.LeaseToken, (Guid?)token)
                        .SetProperty(w => w.ClaimedAt, (DateTime?)now)
                        .SetProperty(w => w.Attempts, w => w.Attempts + 1),
                    cancellationToken);
            if (claimed == 1)
            {
                await FlushClaimedAsync(id, token, now, cancellationToken);
                return true;
            }

            var status = await _db.VoteDigestWindows
                .AsNoTracking()
                .Where(w => w.Id == id)
                .Select(w => (VoteDigestWindowStatus?)w.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (status is null)
            {
                // Redis has a batch the database has never seen. Normally its vote is still being classified and
                // the row appears in a moment; one that stays missing was lost between the two writes.
                var age = TimeSpan.FromMilliseconds(Math.Max(0, redisNow - due.FirstPendingAt));
                if (age >= TimeSpan.FromSeconds(_options.OrphanedPendingSeconds))
                {
                    _logger.LogWarning(
                        "Dropping vote batch {BatchId}: it exists in Redis only, for {AgeSeconds:F0}s.",
                        id,
                        age.TotalSeconds);
                    await CloseInRedisAsync(due.CardKey, id, cancellationToken);
                }

                return false;
            }

            if (status != VoteDigestWindowStatus.Open)
            {
                // The database has already moved on (sent, or being sent): Redis must not offer it again.
                await CloseInRedisAsync(due.CardKey, id, cancellationToken);
            }

            _metrics.ClaimConflicts.Add(1);
            return false;
        }

        private async Task FlushClaimedAsync(Guid windowId, Guid token, DateTime claimedAt, CancellationToken cancellationToken)
        {
            var window = await _db.VoteDigestWindows
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == windowId && w.LeaseToken == token, cancellationToken);
            if (window is null)
            {
                _metrics.ClaimConflicts.Add(1);
                return;
            }

            _metrics.FlushDelayMs.Record(Math.Max(0, (claimedAt - window.EndsAt).TotalMilliseconds));

            // From here on, votes of the card belong to a new batch.
            await CloseInRedisAsync(CardKey.For(window.ContentType, window.ContentId), windowId, cancellationToken);

            var endsAt = window.EndsAt;

            // Eligible: not yet submitted, and not read before the batch's cutoff. A read after the cutoff does not
            // remove an event, so the count is the same whenever the worker gets to the batch.
            Expression<Func<VoteNotificationEvent, bool>> eligible = e =>
                e.WindowId == windowId
                && e.Status == VoteNotificationStatus.Pending
                && _db.Notifications.Any(n => n.Id == e.NotificationId && (!n.IsRead || n.ReadAt > endsAt));

            var count = await _db.VoteNotificationEvents.CountAsync(eligible, cancellationToken);

            var send = new PushSendResult(PushOutcome.Skipped, null);
            if (count > 0)
            {
                var contentType = window.ContentType switch
                {
                    ShareContentType.Poll => "poll",
                    ShareContentType.Personality => "personality",
                    _ => "check"
                };
                send = await _sender.SendAsync(
                    window.OwnerId,
                    PushPayloads.ForBatch(contentType, window.ContentId, count, window.Id),
                    cancellationToken);
            }

            var now = Now();
            var owned = _db.VoteDigestWindows.Where(w => w.Id == windowId && w.LeaseToken == token);
            switch (send.Outcome)
            {
                case PushOutcome.Submitted:
                    var messageId = send.MessageId;
                    await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        var updated = await owned.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(w => w.Status, VoteDigestWindowStatus.Sent)
                                .SetProperty(w => w.EventCount, count)
                                .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                .SetProperty(w => w.ProviderMessageId, messageId)
                                .SetProperty(w => w.LeaseToken, (Guid?)null)
                                .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        if (updated == 0)
                        {
                            // The lease ran out and another worker took the batch over: leave it to them.
                            await transaction.RollbackAsync(cancellationToken);
                            _metrics.ClaimConflicts.Add(1);
                            return;
                        }

                        await _db.VoteNotificationEvents.Where(eligible).ExecuteUpdateAsync(
                            set => set
                                .SetProperty(e => e.Status, VoteNotificationStatus.Submitted)
                                .SetProperty(e => e.SubmittedAt, (DateTime?)now)
                                .SetProperty(e => e.ProviderMessageId, messageId),
                            cancellationToken);
                        await FinishRemainingAsync(windowId, VoteNotificationStatus.Skipped, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }

                    _metrics.BatchesFlushed.Add(1, new KeyValuePair<string, object?>("result", "sent"));
                    _metrics.PushSent.Add(1, new KeyValuePair<string, object?>("kind", "batch"));
                    _metrics.BatchSize.Record(count);
                    _metrics.DeliveryLatencyMs.Record(
                        Math.Max(0, (now - window.StartsAt).TotalMilliseconds),
                        new KeyValuePair<string, object?>("kind", "batch"));
                    break;
                case PushOutcome.Skipped:
                    await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                    {
                        var updated = await owned.ExecuteUpdateAsync(
                            set => set
                                .SetProperty(w => w.Status, VoteDigestWindowStatus.Skipped)
                                .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                .SetProperty(w => w.LeaseToken, (Guid?)null)
                                .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                            cancellationToken);
                        if (updated == 0)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            _metrics.ClaimConflicts.Add(1);
                            return;
                        }

                        await FinishRemainingAsync(windowId, VoteNotificationStatus.Skipped, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }

                    _metrics.BatchesFlushed.Add(1, new KeyValuePair<string, object?>("result", "skipped"));
                    break;
                default:
                    if (window.Attempts >= _options.MaxAttempts)
                    {
                        _logger.LogWarning("Batch push {BatchId} failed permanently after {Attempts} attempts.", windowId, window.Attempts);
                        await using (var transaction = await _db.Database.BeginTransactionAsync(cancellationToken))
                        {
                            var updated = await owned.ExecuteUpdateAsync(
                                set => set
                                    .SetProperty(w => w.Status, VoteDigestWindowStatus.Failed)
                                    .SetProperty(w => w.CompletedAt, (DateTime?)now)
                                    .SetProperty(w => w.LeaseToken, (Guid?)null)
                                    .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                                cancellationToken);
                            if (updated == 0)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return;
                            }

                            await _db.VoteNotificationEvents.Where(eligible).ExecuteUpdateAsync(
                                set => set.SetProperty(e => e.Status, VoteNotificationStatus.Failed),
                                cancellationToken);
                            await FinishRemainingAsync(windowId, VoteNotificationStatus.Skipped, cancellationToken);
                            await transaction.CommitAsync(cancellationToken);
                        }

                        _metrics.PushFailures.Add(
                            1,
                            new KeyValuePair<string, object?>("kind", "batch"),
                            new KeyValuePair<string, object?>("final", true));
                        _metrics.BatchesFlushed.Add(1, new KeyValuePair<string, object?>("result", "failed"));
                        break;
                    }

                    // The batch stays closed: new votes go to a new batch while this one waits for its retry.
                    var retryAt = now + _options.RetryDelay(window.Attempts);
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(w => w.Status, VoteDigestWindowStatus.Due)
                            .SetProperty(w => w.NextAttemptAt, (DateTime?)retryAt)
                            .SetProperty(w => w.LeaseToken, (Guid?)null)
                            .SetProperty(w => w.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    _metrics.PushFailures.Add(
                        1,
                        new KeyValuePair<string, object?>("kind", "batch"),
                        new KeyValuePair<string, object?>("final", false));
                    _metrics.Retries.Add(1, new KeyValuePair<string, object?>("kind", "batch"));
                    _metrics.BatchesFlushed.Add(1, new KeyValuePair<string, object?>("result", "retry"));
                    break;
            }
        }

        /// <summary>Events of the batch that were read before the cutoff (or are otherwise left over) never get a push.</summary>
        private Task<int> FinishRemainingAsync(Guid windowId, VoteNotificationStatus status, CancellationToken cancellationToken)
        {
            return _db.VoteNotificationEvents
                .Where(e => e.WindowId == windowId && e.Status == VoteNotificationStatus.Pending)
                .ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, status), cancellationToken);
        }

        /// <summary>A failure to close in Redis is not fatal: a vote that joins the old batch there finds it closed in the database and moves on.</summary>
        private async Task CloseInRedisAsync(string cardKey, Guid batchId, CancellationToken cancellationToken)
        {
            try
            {
                await _store.CloseBatchAsync(cardKey, batchId, cancellationToken);
            }
            catch (VoteBatchingStoreException exception)
            {
                _metrics.RedisFailures.Add(1);
                _logger.LogWarning(exception, "Could not close vote batch {BatchId} in Redis.", batchId);
            }
        }
    }
}
