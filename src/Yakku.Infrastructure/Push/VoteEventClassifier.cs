using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Redis;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Turns the votes stored as <see cref="VoteNotificationKind.Undecided"/> into either an individual push or a
    /// member of the card's batch. The decision comes from that card's own rolling voting frequency in Redis; no
    /// global counter is involved. The vote is already durable, so a Redis failure only delays it: it is tried again
    /// every few seconds, and after <c>RedisFallbackAfterSeconds</c> it joins a database-only batch instead.
    /// </summary>
    public sealed class VoteEventClassifier
    {
        // Earlier votes of the card sent along with a vote so Redis counts them too. They are the card's immediate
        // votes, which never pass through Redis, and everything a lost Redis forgot. Enough to see every tier
        // threshold; counting is idempotent, so repeating them costs nothing but bytes.
        private const int CompanionLimit = 20;
        private const int MaxReroutes = 3;

        private readonly YakkuDbContext _db;
        private readonly VoteBatchingEngine _engine;
        private readonly VoteBatchingPolicy _policy;
        private readonly IVoteBatchingStore _store;
        private readonly VoteNotificationBatchingOptions _options;
        private readonly VoteNotificationMetrics _metrics;
        private readonly TimeProvider _time;
        private readonly ILogger<VoteEventClassifier> _logger;

        public VoteEventClassifier(
            YakkuDbContext db,
            VoteBatchingEngine engine,
            VoteBatchingPolicy policy,
            IVoteBatchingStore store,
            VoteNotificationBatchingOptions options,
            VoteNotificationMetrics metrics,
            TimeProvider time,
            ILogger<VoteEventClassifier> logger)
        {
            _db = db;
            _engine = engine;
            _policy = policy;
            _store = store;
            _options = options;
            _metrics = metrics;
            _time = time;
            _logger = logger;
        }

        private enum Persisted
        {
            Done,
            WindowClosed,
            LeaseLost
        }

        private DateTime Now() => _time.GetUtcNow().UtcDateTime;

        /// <summary>Claims and classifies up to one batch of undecided votes; returns how many it handled.</summary>
        public async Task<int> ProcessAsync(CancellationToken cancellationToken)
        {
            var now = Now();
            var token = Guid.NewGuid();
            var leaseUntil = now + TimeSpan.FromSeconds(_options.ClassificationLeaseSeconds);
            var limit = _options.ClassificationBatchSize;

            // FOR UPDATE SKIP LOCKED: concurrent workers each take different rows instead of waiting on each other.
            var ids = await _db.Database
                .SqlQuery<Guid>($@"UPDATE vote_notification_events
                                   SET status = 'SENDING', lease_until = {leaseUntil}, lease_token = {token}, attempts = attempts + 1
                                   WHERE id IN (
                                       SELECT id FROM vote_notification_events
                                       WHERE kind = 'UNDECIDED'
                                         AND ((status = 'PENDING' AND next_attempt_at <= {now})
                                              OR (status = 'SENDING' AND lease_until < {now}))
                                       ORDER BY created_at
                                       LIMIT {limit}
                                       FOR UPDATE SKIP LOCKED)
                                   RETURNING id AS ""Value""")
                .ToListAsync(cancellationToken);
            if (ids.Count == 0)
            {
                return 0;
            }

            // Oldest first, so a card's votes are counted in the order they happened.
            var events = await _db.VoteNotificationEvents
                .AsNoTracking()
                .Where(e => ids.Contains(e.Id))
                .OrderBy(e => e.CreatedAt)
                .ThenBy(e => e.Sequence)
                .ToListAsync(cancellationToken);

            foreach (var ev in events)
            {
                await ClassifyAsync(ev, token, cancellationToken);
            }

            return events.Count;
        }

        private async Task ClassifyAsync(VoteNotificationEvent ev, Guid token, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await DecideAndPersistAsync(ev, token, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (VoteBatchingStoreException exception)
            {
                _metrics.RedisFailures.Add(1);
                await HandleFailureAsync(ev, token, exception, redisDown: true, cancellationToken);
            }
            catch (VoteBatchingConflictException exception)
            {
                _metrics.ClassificationConflicts.Add(1);
                await HandleFailureAsync(ev, token, exception, redisDown: false, cancellationToken);
            }
            catch (Exception exception)
            {
                await HandleFailureAsync(ev, token, exception, redisDown: false, cancellationToken);
            }
            finally
            {
                _metrics.ClassificationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private async Task DecideAndPersistAsync(VoteNotificationEvent ev, Guid token, CancellationToken cancellationToken)
        {
            var cardKey = CardKey.For(ev.ContentType, ev.ContentId);
            var card = await _db.VoteNotificationCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContentType == ev.ContentType && c.ContentId == ev.ContentId, cancellationToken);
            var storedMode = card?.DeliveryMode ?? VoteDeliveryMode.Individual;

            var previousVoteAt = await _db.VoteNotificationEvents
                .AsNoTracking()
                .Where(e => e.ContentType == ev.ContentType && e.ContentId == ev.ContentId && e.Sequence < ev.Sequence)
                .OrderByDescending(e => e.Sequence)
                .Select(e => (DateTime?)e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var durableMode = _policy.EffectiveDurableMode(storedMode, previousVoteAt, ev.CreatedAt);

            var windowStart = ev.CreatedAt - _options.FrequencyWindow;
            var companions = (await _db.VoteNotificationEvents
                    .AsNoTracking()
                    .Where(e => e.ContentType == ev.ContentType
                                && e.ContentId == ev.ContentId
                                && e.Sequence < ev.Sequence
                                && e.CreatedAt >= windowStart)
                    .OrderByDescending(e => e.Sequence)
                    .Take(CompanionLimit)
                    .Select(e => new { e.Id, e.CreatedAt })
                    .ToListAsync(cancellationToken))
                .Select(e => new CompanionVote(e.Id, UnixMillis.From(e.CreatedAt)))
                .ToList();

            for (var attempt = 0; attempt <= MaxReroutes; attempt++)
            {
                var decision = await _engine.DecideAsync(
                    cardKey,
                    ev.Id,
                    ev.CreatedAt,
                    durableMode,
                    companions,
                    cancellationToken);

                _metrics.VotesPerMinute.Record(decision.Frequency);
                if (decision.Duplicate)
                {
                    _metrics.DuplicateEvents.Add(1);
                }

                if (decision.Batch is null)
                {
                    await MarkIndividualAsync(ev, token, cancellationToken);
                    await PersistModeAsync(ev, storedMode, decision.Mode, decision.ModeChanged, cancellationToken);
                    _metrics.IndividualCreated.Add(1);
                    _metrics.EventsClassified.Add(1, new KeyValuePair<string, object?>("outcome", "individual"));
                    return;
                }

                var persisted = await PersistInBatchAsync(ev, token, decision.Batch, cancellationToken);
                if (persisted == Persisted.LeaseLost)
                {
                    // Another worker owns this vote now.
                    _metrics.ClaimConflicts.Add(1);
                    return;
                }

                if (persisted == Persisted.Done)
                {
                    await PersistModeAsync(ev, storedMode, decision.Mode, decision.ModeChanged, cancellationToken);
                    _metrics.EventsClassified.Add(1, new KeyValuePair<string, object?>("outcome", "batched"));
                    return;
                }

                // The batch was claimed for sending after Redis last saw it open. Forget it in Redis so the vote
                // starts a new batch there, and decide again.
                await _store.CloseBatchAsync(cardKey, decision.Batch.BatchId, cancellationToken);
            }

            throw new VoteBatchingConflictException(cardKey);
        }

        private Task<int> MarkIndividualAsync(VoteNotificationEvent ev, Guid token, CancellationToken cancellationToken)
        {
            var now = Now();
            // Attempts start again: they now count push attempts, not classification attempts.
            return _db.VoteNotificationEvents
                .Where(e => e.Id == ev.Id && e.LeaseToken == token)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(e => e.Kind, VoteNotificationKind.Immediate)
                        .SetProperty(e => e.WindowId, (Guid?)null)
                        .SetProperty(e => e.Status, VoteNotificationStatus.Pending)
                        .SetProperty(e => e.Attempts, 0)
                        .SetProperty(e => e.NextAttemptAt, (DateTime?)now)
                        .SetProperty(e => e.LeaseToken, (Guid?)null)
                        .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                    cancellationToken);
        }

        /// <summary>
        /// Records the batch in the database and attaches the vote to it in one transaction. The window row is
        /// locked by the update, so a flush cannot claim the batch between "still open" and "vote attached": the
        /// vote is either counted in the push or the batch is reported closed and the vote goes to a new one.
        /// </summary>
        private async Task<Persisted> PersistInBatchAsync(
            VoteNotificationEvent ev,
            Guid token,
            BatchAssignment batch,
            CancellationToken cancellationToken)
        {
            var batchId = batch.BatchId;
            var first = UnixMillis.ToDateTime(batch.FirstPendingAt);
            var last = UnixMillis.ToDateTime(batch.LastVoteAt);
            var next = UnixMillis.ToDateTime(batch.NextFlushAt);
            var max = UnixMillis.ToDateTime(batch.MaxFlushAt);
            var generation = batch.Generation;

            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            Task<int> UpdateOpenAsync() => _db.VoteDigestWindows
                .Where(w => w.Id == batchId && w.Status == VoteDigestWindowStatus.Open)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(w => w.EndsAt, w => w.Generation <= generation ? next : w.EndsAt)
                        .SetProperty(w => w.LastVoteAt, w => w.Generation <= generation ? (DateTime?)last : w.LastVoteAt)
                        .SetProperty(w => w.Generation, w => w.Generation <= generation ? generation : w.Generation),
                    cancellationToken);

            var updated = await UpdateOpenAsync();
            if (updated == 0)
            {
                if (await _db.VoteDigestWindows.AnyAsync(w => w.Id == batchId, cancellationToken))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Persisted.WindowClosed;
                }

                // A card has one open batch. If the database has another one, Redis lost track of it: hand it to
                // the sender (DUE) so its votes still go out, then open this one.
                await _db.VoteDigestWindows
                    .Where(w => w.ContentType == ev.ContentType
                                && w.ContentId == ev.ContentId
                                && w.Status == VoteDigestWindowStatus.Open
                                && w.Id != batchId)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(w => w.Status, VoteDigestWindowStatus.Due)
                            .SetProperty(w => w.NextAttemptAt, (DateTime?)null),
                        cancellationToken);

                var contentTypeName = ev.ContentType.ToString().ToUpperInvariant();
                var inserted = await _db.Database.ExecuteSqlAsync(
                    $@"INSERT INTO vote_digest_windows
                           (id, owner_id, content_type, content_id, starts_at, ends_at, status, attempts, event_count, last_vote_at, max_flush_at, generation)
                       VALUES ({batchId}, {ev.OwnerId}, {contentTypeName}, {ev.ContentId}, {first}, {next}, 'OPEN', 0, 0, {last}, {max}, {generation})
                       ON CONFLICT (id) DO NOTHING",
                    cancellationToken);
                if (inserted == 1)
                {
                    _metrics.BatchesCreated.Add(1);
                }
                else
                {
                    // Another worker inserted the same batch just now.
                    updated = await UpdateOpenAsync();
                    if (updated == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Persisted.WindowClosed;
                    }
                }
            }

            var linked = await _db.VoteNotificationEvents
                .Where(e => e.Id == ev.Id && e.LeaseToken == token)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(e => e.Kind, VoteNotificationKind.Digest)
                        .SetProperty(e => e.WindowId, (Guid?)batchId)
                        .SetProperty(e => e.Status, VoteNotificationStatus.Pending)
                        .SetProperty(e => e.Attempts, 0)
                        .SetProperty(e => e.NextAttemptAt, (DateTime?)null)
                        .SetProperty(e => e.LeaseToken, (Guid?)null)
                        .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                    cancellationToken);
            if (linked == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Persisted.LeaseLost;
            }

            await transaction.CommitAsync(cancellationToken);
            return Persisted.Done;
        }

        private async Task PersistModeAsync(
            VoteNotificationEvent ev,
            VoteDeliveryMode stored,
            VoteDeliveryMode mode,
            bool modeChanged,
            CancellationToken cancellationToken)
        {
            if (modeChanged)
            {
                _metrics.ModeChanges.Add(1, new KeyValuePair<string, object?>("mode", mode.ToString().ToLowerInvariant()));
            }

            if (mode == stored)
            {
                return;
            }

            var now = Now();
            await _db.VoteNotificationCards
                .Where(c => c.ContentType == ev.ContentType && c.ContentId == ev.ContentId)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(c => c.DeliveryMode, mode)
                        .SetProperty(c => c.UpdatedAt, now),
                    cancellationToken);
        }

        private async Task HandleFailureAsync(
            VoteNotificationEvent ev,
            Guid token,
            Exception exception,
            bool redisDown,
            CancellationToken cancellationToken)
        {
            var now = Now();
            var waiting = now - ev.CreatedAt;
            if (redisDown && waiting >= TimeSpan.FromSeconds(_options.RedisFallbackAfterSeconds))
            {
                try
                {
                    if (await FallbackToDatabaseAsync(ev, token, cancellationToken))
                    {
                        _metrics.RedisFallbacks.Add(1);
                        _metrics.EventsClassified.Add(1, new KeyValuePair<string, object?>("outcome", "fallback"));
                        _logger.LogWarning(
                            "Redis unavailable for {WaitSeconds:F0}s; vote event {EventId} joined a database-only batch.",
                            waiting.TotalSeconds,
                            ev.Id);
                        return;
                    }
                }
                catch (Exception fallbackException) when (fallbackException is not OperationCanceledException)
                {
                    _logger.LogError(fallbackException, "Database-only fallback failed for vote event {EventId}.", ev.Id);
                }
            }

            _logger.LogWarning(
                exception,
                "Vote event {EventId} could not be classified ({Reason}); trying again in {RetrySeconds}s.",
                ev.Id,
                redisDown ? "redis" : "error",
                _options.ClassificationRetrySeconds);

            var retryAt = now + TimeSpan.FromSeconds(_options.ClassificationRetrySeconds);
            await _db.VoteNotificationEvents
                .Where(e => e.Id == ev.Id && e.LeaseToken == token)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(e => e.Status, VoteNotificationStatus.Pending)
                        .SetProperty(e => e.NextAttemptAt, (DateTime?)retryAt)
                        .SetProperty(e => e.LeaseToken, (Guid?)null)
                        .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                    CancellationToken.None);
        }

        /// <summary>
        /// Redis has been unavailable for too long: attach the vote to the card's open database batch, or open one
        /// that is due after the maximum wait. The database sweeper sends it, so nothing waits on Redis.
        /// </summary>
        private async Task<bool> FallbackToDatabaseAsync(VoteNotificationEvent ev, Guid token, CancellationToken cancellationToken)
        {
            var open = await _db.VoteDigestWindows
                .AsNoTracking()
                .Where(w => w.ContentType == ev.ContentType
                            && w.ContentId == ev.ContentId
                            && w.Status == VoteDigestWindowStatus.Open)
                .FirstOrDefaultAsync(cancellationToken);

            BatchAssignment assignment;
            if (open is not null)
            {
                assignment = new BatchAssignment(
                    open.Id,
                    UnixMillis.From(open.StartsAt),
                    UnixMillis.From(open.LastVoteAt ?? open.StartsAt),
                    UnixMillis.From(open.EndsAt),
                    UnixMillis.From(open.MaxFlushAt ?? open.EndsAt),
                    open.Generation,
                    0);
            }
            else
            {
                var first = UnixMillis.From(ev.CreatedAt);
                var max = _policy.MaxFlushAt(first);
                assignment = new BatchAssignment(Guid.NewGuid(), first, first, max, max, 1, 1);
            }

            return await PersistInBatchAsync(ev, token, assignment, cancellationToken) == Persisted.Done;
        }
    }
}
