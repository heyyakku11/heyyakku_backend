using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.PushNotifications;
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

        public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return _semaphore.WaitAsync(timeout, cancellationToken);
        }
    }

    public sealed class VoteNotificationOutbox : IVoteNotificationOutbox
    {
        private readonly YakkuDbContext _context;
        private readonly VoteNotificationSignal _signal;

        public VoteNotificationOutbox(YakkuDbContext context, VoteNotificationSignal signal)
        {
            _context = context;
            _signal = signal;
        }

        public void Wake() => _signal.Notify();

        public async Task RecordAsync(
            VotableContent content,
            VoteReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            var contentType = content.ContentType;
            var contentTypeName = contentType.ToString().ToUpperInvariant();

            // One atomic upsert: it takes the card's row lock until the vote commits, so concurrent votes on the
            // card are numbered 1, 2, 3, ... without gaps or repeats, and a rolled-back vote gives its number back.
            var counts = await _context.Database
                .SqlQuery<int>($@"INSERT INTO vote_notification_cards (content_type, content_id, owner_id, vote_count)
                                  VALUES ({contentTypeName}, {content.ContentId}, {content.OwnerId}, 1)
                                  ON CONFLICT (content_type, content_id)
                                  DO UPDATE SET vote_count = vote_notification_cards.vote_count + 1
                                  RETURNING vote_count AS ""Value""")
                .ToListAsync(cancellationToken);
            var sequence = counts[0];

            var noun = content.IsPoll ? "poll" : "check";
            var notification = new Notification(
                content.OwnerId,
                NotificationType.Normal,
                NotificationEventType.PollAnswered,
                "New answer",
                $"Someone answered your {noun}.",
                data: JsonSerializer.Serialize(new
                {
                    contentId = content.ContentId,
                    contentType = noun,
                    shareCode = content.ShareCode
                }));
            _context.Notifications.Add(notification);

            var now = receipt.CreatedAt;
            var window = await _context.VoteDigestWindows.FirstOrDefaultAsync(
                item => item.ContentType == contentType
                        && item.ContentId == content.ContentId
                        && item.Status == VoteDigestWindowStatus.Open,
                cancellationToken);

            var plan = VoteNotificationPlanner.Decide(sequence, window?.EndsAt, now);
            if (plan.CloseOpenWindow && window is not null)
            {
                window.MarkDue();
                // The unique open-window index needs the old row out of 'OPEN' before the new one goes in.
                await _context.SaveChangesAsync(cancellationToken);
                window = null;
            }

            if (plan.OpenNewWindow)
            {
                window = VoteDigestWindow.Open(
                    content.OwnerId,
                    contentType,
                    content.ContentId,
                    now,
                    VoteNotificationOptions.DigestWindow);
                _context.VoteDigestWindows.Add(window);
            }

            _context.VoteNotificationEvents.Add(plan.Kind == VoteNotificationKind.Immediate
                ? VoteNotificationEvent.Immediate(
                    content.OwnerId,
                    contentType,
                    content.ContentId,
                    receipt.VoteId,
                    sequence,
                    notification.Id,
                    now)
                : VoteNotificationEvent.ForDigest(
                    content.OwnerId,
                    contentType,
                    content.ContentId,
                    receipt.VoteId,
                    sequence,
                    notification.Id,
                    window!.Id,
                    now));

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
