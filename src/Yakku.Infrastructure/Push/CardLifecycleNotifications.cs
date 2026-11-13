using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Writes the close and personality inbox rows, and queues the same generic push the vote outbox sends.
    /// The activity list reads the title and body stored here. The push itself stays generic.
    /// </summary>
    public sealed class CardLifecycleNotifications : ICardLifecycleNotifications
    {
        private static readonly TimeSpan Lookback = TimeSpan.FromDays(7);

        private readonly YakkuDbContext _db;
        private readonly VoteNotificationSignal _signal;
        private readonly ILogger<CardLifecycleNotifications> _logger;

        public CardLifecycleNotifications(
            YakkuDbContext db,
            VoteNotificationSignal signal,
            ILogger<CardLifecycleNotifications> logger)
        {
            _db = db;
            _signal = signal;
            _logger = logger;
        }

        public void Wake() => _signal.Notify();

        public async Task NotifyPollClosedAsync(Guid pollId, CancellationToken cancellationToken = default)
        {
            try
            {
                await NotifyPollClosedCoreAsync(pollId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not record the close notification for poll {PollId}.", pollId);
            }
        }

        public async Task NotifyCheckClosedAsync(Guid userCheckId, CancellationToken cancellationToken = default)
        {
            try
            {
                await NotifyCheckClosedCoreAsync(userCheckId, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not record the close notification for check {UserCheckId}.", userCheckId);
            }
        }

        public void StagePersonalityReady(Guid userId, Guid cardId, int friendVotes, string shareCode)
        {
            var copy = CardActivityCopy.ForPersonality(friendVotes);
            Stage(
                userId,
                NotificationEventType.PersonalityReady,
                copy.Title,
                copy.Body,
                ShareContentType.Personality,
                "personality",
                cardId,
                shareCode,
                friendVotes);
        }

        public async Task NotifyExpiredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var since = utcNow - Lookback;
            var pollIds = await _db.Polls
                .AsNoTracking()
                .Where(poll => poll.Status == PollStatus.Active
                               && poll.ExpiresAt != null
                               && poll.ExpiresAt <= utcNow
                               && poll.ExpiresAt >= since)
                .OrderBy(poll => poll.ExpiresAt)
                .Select(poll => poll.Id)
                .Take(40)
                .ToListAsync(cancellationToken);

            foreach (var pollId in pollIds)
            {
                await NotifyPollClosedAsync(pollId, cancellationToken);
            }

            var checkIds = await _db.UserChecks
                .AsNoTracking()
                .Where(item => item.ClosedAt == null
                               && item.ExpiresAt != null
                               && item.ExpiresAt <= utcNow
                               && item.ExpiresAt >= since)
                .OrderBy(item => item.ExpiresAt)
                .Select(item => item.Id)
                .Take(40)
                .ToListAsync(cancellationToken);

            foreach (var checkId in checkIds)
            {
                await NotifyCheckClosedAsync(checkId, cancellationToken);
            }
        }

        private async Task NotifyPollClosedCoreAsync(Guid pollId, CancellationToken cancellationToken)
        {
            if (await AlreadySentAsync("PollClosed", "poll", pollId, cancellationToken))
            {
                return;
            }

            var poll = await _db.Polls.AsNoTracking().FirstOrDefaultAsync(item => item.Id == pollId, cancellationToken);
            if (poll is null)
            {
                return;
            }

            var friendVotes = await _db.PollVotes.CountAsync(
                vote => vote.PollId == poll.Id && vote.VoterId != poll.CreatorId,
                cancellationToken);
            var copy = CardActivityCopy.ForClosed(
                poll.Question,
                friendVotes,
                OperationalSettings.Results.MinVotesToReveal);
            await SaveAsync(
                poll.CreatorId,
                NotificationEventType.PollClosed,
                copy.Title,
                copy.Body,
                ShareContentType.Poll,
                "poll",
                poll.Id,
                poll.ShareCode,
                friendVotes,
                cancellationToken);
        }

        private async Task NotifyCheckClosedCoreAsync(Guid userCheckId, CancellationToken cancellationToken)
        {
            if (await AlreadySentAsync("PollClosed", "check", userCheckId, cancellationToken))
            {
                return;
            }

            var item = await _db.UserChecks
                .AsNoTracking()
                .Include(check => check.Check)
                .Include(check => check.Votes)
                .FirstOrDefaultAsync(check => check.Id == userCheckId, cancellationToken);
            if (item is null)
            {
                return;
            }

            var friendVotes = item.Votes.Count(vote => vote.VoterId != item.UserId);
            var question = string.IsNullOrWhiteSpace(item.Check.Question) ? item.Check.Title : item.Check.Question;
            var copy = CardActivityCopy.ForClosed(
                question,
                friendVotes,
                OperationalSettings.Results.MinVotesToReveal);
            await SaveAsync(
                item.UserId,
                NotificationEventType.PollClosed,
                copy.Title,
                copy.Body,
                ShareContentType.Check,
                "check",
                item.Id,
                item.ShareCode,
                friendVotes,
                cancellationToken);
        }

        private void Stage(
            Guid userId,
            NotificationEventType eventType,
            string title,
            string body,
            ShareContentType contentType,
            string contentTypeName,
            Guid contentId,
            string? shareCode,
            int sequence)
        {
            var notification = InboxRow(userId, eventType, title, body, contentTypeName, contentId, shareCode);
            _db.Notifications.Add(notification);
            _db.VoteNotificationEvents.Add(VoteNotificationEvent.Immediate(
                userId,
                contentType,
                contentId,
                notification.Id,
                sequence,
                notification.Id,
                DateTime.UtcNow));
        }

        private async Task SaveAsync(
            Guid userId,
            NotificationEventType eventType,
            string title,
            string body,
            ShareContentType contentType,
            string contentTypeName,
            Guid contentId,
            string? shareCode,
            int sequence,
            CancellationToken cancellationToken)
        {
            Stage(userId, eventType, title, body, contentType, contentTypeName, contentId, shareCode, sequence);
            await _db.SaveChangesAsync(cancellationToken);
            Wake();
        }

        private static Notification InboxRow(
            Guid userId,
            NotificationEventType eventType,
            string title,
            string body,
            string contentTypeName,
            Guid contentId,
            string? shareCode)
        {
            return new Notification(
                userId,
                NotificationType.Normal,
                eventType,
                title,
                body,
                data: JsonSerializer.Serialize(new
                {
                    contentId,
                    contentType = contentTypeName,
                    shareCode
                }));
        }

        private async Task<bool> AlreadySentAsync(
            string eventType,
            string contentType,
            Guid contentId,
            CancellationToken cancellationToken)
        {
            var id = contentId.ToString();
            var found = await _db.Database.SqlQuery<int>(
                $"""
                SELECT 1 AS "Value"
                FROM notifications
                WHERE "EventType" = {eventType}
                  AND "Data" IS NOT NULL
                  AND "Data"->>'contentType' = {contentType}
                  AND "Data"->>'contentId' = {id}
                LIMIT 1
                """)
                .ToListAsync(cancellationToken);
            return found.Count > 0;
        }
    }
}
