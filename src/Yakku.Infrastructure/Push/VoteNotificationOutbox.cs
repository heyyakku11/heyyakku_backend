using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.Common;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Sharing;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Push
{
    /// <summary>Wakes the dispatcher; losing a wake-up only delays a push until the next poll.</summary>
    public sealed class VoteNotificationSignal
    {
        private readonly SemaphoreSlim _semaphore = new(0, 1);

        public void Notify()
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already signalled.
            }
        }

        /// <summary>Waits for a wake-up; returns true when one arrived and false when the timeout passed.</summary>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return _semaphore.WaitAsync(timeout, cancellationToken);
        }
    }

    public sealed class VoteNotificationOutbox : IVoteNotificationOutbox
    {
        private readonly YakkuDbContext _context;
        private readonly VoteNotificationSignal _signal;
        private readonly VoteNotificationBatchingOptions _options;

        public VoteNotificationOutbox(
            YakkuDbContext context,
            VoteNotificationSignal signal,
            VoteNotificationBatchingOptions options)
        {
            _context = context;
            _signal = signal;
            _options = options;
        }

        public void Wake() => _signal.Notify();

        public Task RecordAsync(
            VotableContent content,
            VoteReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            return StoreAsync(
                content.ContentType,
                content.ContentId,
                content.OwnerId,
                content.ShareCode,
                content.IsPoll ? "poll" : "check",
                receipt,
                sequence => CardActivityCopy.ForVote(
                    content.Question,
                    sequence,
                    OperationalSettings.Results.MinVotesToReveal,
                    content.ExpiresAt,
                    receipt.CreatedAt),
                cancellationToken);
        }

        public Task RecordPersonalityVoteAsync(
            Guid ownerId,
            Guid cardId,
            string shareCode,
            VoteReceipt receipt,
            int totalVotes,
            int requiredVotes,
            CancellationToken cancellationToken = default)
        {
            // The inbox line shows the card's own progress, not the outbox counter: a card that already had votes
            // before this pipeline covered personality cards is numbered from its first recorded vote here.
            return StoreAsync(
                ShareContentType.Personality,
                cardId,
                ownerId,
                shareCode,
                "personality",
                receipt,
                _ => CardActivityCopy.ForPersonalityVote(totalVotes, requiredVotes),
                cancellationToken);
        }

        private async Task StoreAsync(
            ShareContentType contentType,
            Guid contentId,
            Guid ownerId,
            string shareCode,
            string noun,
            VoteReceipt receipt,
            Func<int, (string Title, string Body)> copyFor,
            CancellationToken cancellationToken)
        {
            var contentTypeName = contentType.ToString().ToUpperInvariant();
            var now = DateTime.SpecifyKind(receipt.CreatedAt, DateTimeKind.Utc);

            // One atomic upsert: it takes the card's row lock until the vote commits, so concurrent votes on the
            // card are numbered 1, 2, 3, ... without gaps or repeats, and a rolled-back vote gives its number back.
            // This durable counter, not Redis, decides which votes are the card's immediate ones.
            var counts = await _context.Database
                .SqlQuery<int>($@"INSERT INTO vote_notification_cards (content_type, content_id, owner_id, vote_count, delivery_mode, last_vote_at, updated_at)
                                  VALUES ({contentTypeName}, {contentId}, {ownerId}, 1, 'INDIVIDUAL', {now}, {now})
                                  ON CONFLICT (content_type, content_id)
                                  DO UPDATE SET vote_count = vote_notification_cards.vote_count + 1,
                                                last_vote_at = GREATEST(COALESCE(vote_notification_cards.last_vote_at, EXCLUDED.last_vote_at), EXCLUDED.last_vote_at),
                                                updated_at = EXCLUDED.updated_at
                                  RETURNING vote_count AS ""Value""")
                .ToListAsync(cancellationToken);
            var sequence = counts[0];

            var copy = copyFor(sequence);
            var notification = new Notification(
                ownerId,
                NotificationType.Normal,
                NotificationEventType.PollAnswered,
                copy.Title,
                copy.Body,
                data: JsonSerializer.Serialize(new
                {
                    contentId,
                    contentType = noun,
                    shareCode
                }));
            _context.Notifications.Add(notification);

            // The first votes are pushed at once. Later ones are stored as undecided: the dispatcher classifies
            // them from the card's recent voting frequency after the commit, so the vote never waits on Redis.
            _context.VoteNotificationEvents.Add(
                VoteNotificationPlanner.Decide(sequence, _options.ImmediateVoteLimit) == VoteNotificationKind.Immediate
                    ? VoteNotificationEvent.Immediate(
                        ownerId,
                        contentType,
                        contentId,
                        receipt.VoteId,
                        sequence,
                        notification.Id,
                        now)
                    : VoteNotificationEvent.Undecided(
                        ownerId,
                        contentType,
                        contentId,
                        receipt.VoteId,
                        sequence,
                        notification.Id,
                        now));

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}