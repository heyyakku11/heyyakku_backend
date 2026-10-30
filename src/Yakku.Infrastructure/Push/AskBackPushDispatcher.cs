using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.AskBack;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Sends the push rows written with each Ask Back message. Rows are claimed with a conditional update and a
    /// lease (like the vote notification outbox), so several instances can run it and a crashed claim becomes
    /// claimable again. A recipient who is viewing the conversation, or who muted it, gets no push.
    /// </summary>
    public sealed class AskBackPushDispatcher
    {
        private readonly YakkuDbContext _db;
        private readonly IPushNotificationService _push;
        private readonly IAskBackPresence _presence;
        private readonly TimeProvider _time;
        private readonly ILogger<AskBackPushDispatcher> _logger;

        public AskBackPushDispatcher(
            YakkuDbContext db,
            IPushNotificationService push,
            IAskBackPresence presence,
            TimeProvider time,
            ILogger<AskBackPushDispatcher> logger)
        {
            _db = db;
            _push = push;
            _presence = presence;
            _time = time;
            _logger = logger;
        }

        public async Task<int> ProcessAsync(CancellationToken cancellationToken)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var ids = await _db.AskBackOutbox
                .AsNoTracking()
                .Where(e => (e.Status == AskBackOutboxStatus.Pending && e.NextAttemptAt <= now)
                            || (e.Status == AskBackOutboxStatus.Processing && e.LeaseUntil < now))
                .OrderBy(e => e.NextAttemptAt)
                .Select(e => e.Id)
                .Take(AskBackOptions.BatchSize)
                .ToListAsync(cancellationToken);

            var handled = 0;
            foreach (var id in ids)
            {
                var token = Guid.NewGuid();
                var leaseUntil = now + AskBackOptions.Lease;
                var claimed = await _db.AskBackOutbox
                    .Where(e => e.Id == id
                                && ((e.Status == AskBackOutboxStatus.Pending && e.NextAttemptAt <= now)
                                    || (e.Status == AskBackOutboxStatus.Processing && e.LeaseUntil < now)))
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, AskBackOutboxStatus.Processing)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)leaseUntil)
                            .SetProperty(e => e.LeaseToken, (Guid?)token)
                            .SetProperty(e => e.Attempts, e => e.Attempts + 1),
                        cancellationToken);
                if (claimed == 0)
                {
                    continue;
                }

                handled++;
                await SendAsync(id, token, cancellationToken);
            }

            return handled;
        }

        private async Task SendAsync(Guid id, Guid token, CancellationToken cancellationToken)
        {
            var row = await _db.AskBackOutbox.AsNoTracking().FirstAsync(e => e.Id == id, cancellationToken);

            var outcome = await DecideAndSendAsync(row.ConversationId, row.RecipientUserId, cancellationToken);

            var now = _time.GetUtcNow().UtcDateTime;
            var owned = _db.AskBackOutbox.Where(e => e.Id == id && e.LeaseToken == token);
            switch (outcome)
            {
                case PushOutcome.Submitted:
                    await Finish(owned, AskBackOutboxStatus.Sent, now, cancellationToken);
                    break;
                case PushOutcome.Skipped:
                    await Finish(owned, AskBackOutboxStatus.Skipped, now, cancellationToken);
                    break;
                default:
                    if (row.Attempts >= PushQueueOptions.MaxAttempts)
                    {
                        _logger.LogWarning("Ask Back push {EventId} failed permanently.", id);
                        await Finish(owned, AskBackOutboxStatus.Failed, now, cancellationToken);
                        break;
                    }

                    var retryAt = now + PushQueueOptions.BackoffWithJitter(row.Attempts, Random.Shared.NextDouble());
                    await owned.ExecuteUpdateAsync(
                        set => set
                            .SetProperty(e => e.Status, AskBackOutboxStatus.Pending)
                            .SetProperty(e => e.NextAttemptAt, retryAt)
                            .SetProperty(e => e.LeaseToken, (Guid?)null)
                            .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                        cancellationToken);
                    break;
            }
        }

        private async Task<PushOutcome> DecideAndSendAsync(
            Guid conversationId,
            Guid recipientId,
            CancellationToken cancellationToken)
        {
            if (_presence.IsViewing(conversationId, recipientId))
            {
                return PushOutcome.Skipped;
            }

            var muted = await _db.AskBackParticipants
                .AsNoTracking()
                .AnyAsync(
                    p => p.ConversationId == conversationId && p.UserId == recipientId && p.IsMuted,
                    cancellationToken);
            if (muted)
            {
                return PushOutcome.Skipped;
            }

            try
            {
                var result = await _push.SendToUserAsync(
                    recipientId, PushPayloads.ForAskBackMessage(conversationId), cancellationToken);
                return VoteNotificationDispatcher.Classify(result, null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var outcome = VoteNotificationDispatcher.Classify(null, exception);
                if (outcome == PushOutcome.Retry)
                {
                    _logger.LogWarning(exception, "Ask Back push failed for conversation {ConversationId}.", conversationId);
                }

                return outcome;
            }
        }

        private static Task<int> Finish(
            IQueryable<Domain.Entities.AskBackOutboxEvent> owned,
            AskBackOutboxStatus status,
            DateTime now,
            CancellationToken cancellationToken)
        {
            return owned.ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.Status, status)
                    .SetProperty(e => e.CompletedAt, (DateTime?)now)
                    .SetProperty(e => e.LeaseToken, (Guid?)null)
                    .SetProperty(e => e.LeaseUntil, (DateTime?)null),
                cancellationToken);
        }
    }
}
