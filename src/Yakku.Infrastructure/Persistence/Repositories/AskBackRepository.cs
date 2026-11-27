using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.AskBack;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AskBackRepository : IAskBackRepository
    {
        private const string VoteConstraint = "IX_ask_back_conversations_vote_id";
        private const string IdempotencyConstraint = "IX_ask_back_messages_idempotency";

        private readonly YakkuDbContext _context;

        public AskBackRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<AskBackVoteContext?> FindVoteContextAsync(
            Guid voteId,
            CancellationToken cancellationToken = default)
        {
            var pollVote = await _context.PollVotes
                .AsNoTracking()
                .Where(vote => vote.Id == voteId)
                .Select(vote => new
                {
                    vote.PollId,
                    OwnerId = vote.Poll.CreatorId,
                    vote.VoterId,
                    vote.VoterRole,
                    HasReason = vote.Reason != null
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (pollVote is not null)
            {
                var otherVotes = await _context.PollVotes
                    .AsNoTracking()
                    .CountAsync(vote => vote.PollId == pollVote.PollId && vote.VoterId != pollVote.OwnerId, cancellationToken);
                return new AskBackVoteContext(
                    ShareContentType.Poll,
                    pollVote.PollId,
                    voteId,
                    pollVote.OwnerId,
                    pollVote.VoterId,
                    pollVote.VoterRole,
                    pollVote.HasReason,
                    otherVotes);
            }

            var checkVote = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(vote => vote.Id == voteId)
                .Select(vote => new
                {
                    vote.UserCheckId,
                    OwnerId = vote.UserCheck.UserId,
                    vote.VoterId,
                    vote.VoterRole,
                    HasReason = vote.Reason != null
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (checkVote is null)
            {
                return null;
            }

            var otherCheckVotes = await _context.UserCheckVotes
                .AsNoTracking()
                .CountAsync(vote => vote.UserCheckId == checkVote.UserCheckId && vote.VoterId != checkVote.OwnerId, cancellationToken);
            return new AskBackVoteContext(
                ShareContentType.Check,
                checkVote.UserCheckId,
                voteId,
                checkVote.OwnerId,
                checkVote.VoterId,
                checkVote.VoterRole,
                checkVote.HasReason,
                otherCheckVotes);
        }

        public async Task<HashSet<Guid>> GetUsableUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            var ids = userIds.ToArray();
            var usable = await _context.Users
                .AsNoTracking()
                .Where(user => ids.Contains(user.Id)
                               && user.Status == UserStatus.Active
                               && user.DeletionRequest == null)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            return usable.ToHashSet();
        }

        public async Task<bool> AcceptsAskBacksAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // A user with no permissions row cannot be asked back either.
            return await _context.UserPermissions
                .AsNoTracking()
                .AnyAsync(permission => permission.UserId == userId && permission.AllowAskBack, cancellationToken);
        }

        public Task<string?> GetAnonymousNameAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => (string?)user.AnonymousName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public Task<Guid?> FindConversationIdByVoteAsync(Guid voteId, CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .AsNoTracking()
                .Where(conversation => conversation.VoteId == voteId)
                .Select(conversation => (Guid?)conversation.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<(Guid ConversationId, bool Created)> AddOrGetAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken = default)
        {
            // The conversation and its two participants are one SaveChanges, so one transaction.
            _context.AskBackConversations.Add(conversation);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return (conversation.Id, true);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, VoteConstraint))
            {
                // A concurrent request created it first: the unique index on vote_id decides.
                _context.ChangeTracker.Clear();
                var existing = await FindConversationIdByVoteAsync(conversation.VoteId, cancellationToken);
                return (existing ?? throw exception, false);
            }
        }

        public Task<AskBackConversation?> FindForParticipantAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .Include(conversation => conversation.Participants)
                .FirstOrDefaultAsync(
                    conversation => conversation.Id == conversationId
                                    && (conversation.CreatorUserId == userId || conversation.VoterUserId == userId),
                    cancellationToken);
        }

        public async Task<IReadOnlyList<AskBackConversation>> FindForParticipantsAsync(
            IReadOnlyCollection<Guid> conversationIds,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            if (conversationIds.Count == 0)
            {
                return Array.Empty<AskBackConversation>();
            }

            return await _context.AskBackConversations
                .Include(conversation => conversation.Participants)
                .Where(conversation => conversationIds.Contains(conversation.Id)
                    && (conversation.CreatorUserId == userId || conversation.VoterUserId == userId))
                .ToListAsync(cancellationToken);
        }

        public Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .AsNoTracking()
                .AnyAsync(
                    conversation => conversation.Id == conversationId
                                    && (conversation.CreatorUserId == userId || conversation.VoterUserId == userId),
                    cancellationToken);
        }

        public Task<AskBackConversation?> GetSnapshotAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    conversation => conversation.Id == conversationId
                                    && (conversation.CreatorUserId == userId || conversation.VoterUserId == userId),
                    cancellationToken);
        }

        public async Task<AskBackConversationResponse?> GetDetailsAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _context.AskBackConversations
                .AsNoTracking()
                .Include(item => item.Participants)
                .FirstOrDefaultAsync(
                    item => item.Id == conversationId && (item.CreatorUserId == userId || item.VoterUserId == userId),
                    cancellationToken);
            if (conversation is null)
            {
                return null;
            }

            var isAsker = conversation.CreatorUserId == userId;
            var otherId = conversation.OtherParticipant(userId);
            var names = await LoadNamesAsync(otherId is { } id ? [id] : [], cancellationToken);
            return new AskBackConversationResponse
            {
                Id = conversation.Id,
                VoteId = conversation.VoteId,
                Status = StatusName(conversation.Status),
                MyRole = isAsker ? "asker" : "voter",
                Other = ToParticipant(otherId, names),
                Context = await LoadContextAsync(conversation, cancellationToken),
                CreatedAt = conversation.CreatedAt,
                ClosedAt = conversation.ClosedAt,
                UnreadCount = await CountUnreadAsync(conversation.Id, userId, cancellationToken),
                CanSend = conversation.CanSend,
                CanReopen = conversation.Status == AskBackStatus.Closed && isAsker,
                CanUnblock = conversation.Status == AskBackStatus.Blocked && conversation.BlockedByUserId == userId
            };
        }

        public async Task<AskBackPage<AskBackConversationListItemResponse>> ListAsync(
            Guid userId,
            (DateTime Timestamp, Guid Id)? cursor,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.AskBackConversations
                .AsNoTracking()
                .Where(conversation => conversation.CreatorUserId == userId || conversation.VoterUserId == userId);
            if (cursor is { } position)
            {
                query = query.Where(conversation =>
                    conversation.LastActivityAt < position.Timestamp
                    || (conversation.LastActivityAt == position.Timestamp && conversation.Id.CompareTo(position.Id) < 0));
            }

            var rows = await query
                .OrderByDescending(conversation => conversation.LastActivityAt)
                .ThenByDescending(conversation => conversation.Id)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);

            var hasMore = rows.Count > pageSize;
            rows = rows.Take(pageSize).ToList();
            var ids = rows.Select(row => row.Id).ToArray();

            var otherIds = rows
                .Select(row => row.OtherParticipant(userId))
                .Where(other => other.HasValue)
                .Select(other => other!.Value)
                .Distinct()
                .ToArray();
            var names = await LoadNamesAsync(otherIds, cancellationToken);

            // The newest message of each conversation, in one query.
            var lastMessages = (await _context.AskBackMessages
                    .AsNoTracking()
                    .Where(message => ids.Contains(message.ConversationId)
                                      && message.DeletedAt == null
                                      && message.Id == _context.AskBackMessages
                                          .Where(other => other.ConversationId == message.ConversationId && other.DeletedAt == null)
                                          .OrderByDescending(other => other.CreatedAt)
                                          .ThenByDescending(other => other.Id)
                                          .Select(other => other.Id)
                                          .First())
                    .Select(message => new { message.ConversationId, message.Content, message.CreatedAt, message.SenderUserId })
                    .ToListAsync(cancellationToken))
                .ToDictionary(message => message.ConversationId);

            var unread = await (
                    from message in _context.AskBackMessages.AsNoTracking()
                    join participant in _context.AskBackParticipants.AsNoTracking()
                        on message.ConversationId equals participant.ConversationId
                    where participant.UserId == userId
                          && ids.Contains(message.ConversationId)
                          && message.DeletedAt == null
                          && message.SenderUserId != userId
                          && (participant.LastReadMessageCreatedAt == null
                              || message.CreatedAt > participant.LastReadMessageCreatedAt
                              || (message.CreatedAt == participant.LastReadMessageCreatedAt
                                  && message.Id.CompareTo(participant.LastReadMessageId!.Value) > 0))
                    group message by message.ConversationId into grouped
                    select new { ConversationId = grouped.Key, Count = grouped.Count() })
                .ToDictionaryAsync(item => item.ConversationId, item => item.Count, cancellationToken);

            var contexts = await LoadListContextsAsync(rows, cancellationToken);

            var items = rows.Select(row =>
            {
                lastMessages.TryGetValue(row.Id, out var last);
                contexts.TryGetValue(row.Id, out var context);
                return new AskBackConversationListItemResponse
                {
                    Id = row.Id,
                    Status = StatusName(row.Status),
                    MyRole = row.CreatorUserId == userId ? "asker" : "voter",
                    Other = ToParticipant(row.OtherParticipant(userId), names),
                    Context = context ?? new AskBackContextResponse
                    {
                        ContentType = row.ContentType == ShareContentType.Poll ? "poll" : "check",
                        ContentId = row.ContentId,
                        Title = row.ContentType == ShareContentType.Poll ? "Question" : null
                    },
                    LastMessage = last is null
                        ? null
                        : new AskBackMessagePreviewResponse
                        {
                            Preview = Preview(last.Content),
                            IsMine = last.SenderUserId == userId,
                            CreatedAt = last.CreatedAt
                        },
                    LastMessageAt = row.LastMessageAt,
                    LastActivityAt = row.LastActivityAt,
                    UnreadCount = unread.GetValueOrDefault(row.Id),
                    CreatedAt = row.CreatedAt,
                    CanUnblock = row.Status == AskBackStatus.Blocked && row.BlockedByUserId == userId
                };
            }).ToList();

            var next = hasMore && rows.Count > 0
                ? AskBackCursor.Encode(rows[^1].LastActivityAt, rows[^1].Id)
                : null;
            return new AskBackPage<AskBackConversationListItemResponse>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = next
            };
        }

        /// <summary>Titles and picked options for a page of conversations, in a few queries.</summary>
        private async Task<Dictionary<Guid, AskBackContextResponse>> LoadListContextsAsync(
            List<AskBackConversation> rows,
            CancellationToken cancellationToken)
        {
            var result = new Dictionary<Guid, AskBackContextResponse>();
            if (rows.Count == 0)
            {
                return result;
            }

            var pollIds = rows
                .Where(row => row.ContentType == ShareContentType.Poll)
                .Select(row => row.ContentId)
                .Distinct()
                .ToArray();
            var checkIds = rows
                .Where(row => row.ContentType == ShareContentType.Check)
                .Select(row => row.ContentId)
                .Distinct()
                .ToArray();
            var pollVoteIds = rows
                .Where(row => row.ContentType == ShareContentType.Poll)
                .Select(row => row.VoteId)
                .ToArray();
            var checkVoteIds = rows
                .Where(row => row.ContentType == ShareContentType.Check)
                .Select(row => row.VoteId)
                .ToArray();

            var pollQuestions = pollIds.Length == 0
                ? new Dictionary<Guid, string?>()
                : await _context.Polls.AsNoTracking()
                    .Where(poll => pollIds.Contains(poll.Id))
                    .ToDictionaryAsync(poll => poll.Id, poll => (string?)poll.Question, cancellationToken);
            var checkMeta = checkIds.Length == 0
                ? new Dictionary<Guid, (string? Title, string? Question)>()
                : (await _context.UserChecks.AsNoTracking()
                        .Where(item => checkIds.Contains(item.Id))
                        .Select(item => new { item.Id, item.Check.Title, item.Check.Question })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(
                        item => item.Id,
                        item => (Title: (string?)item.Title, Question: (string?)item.Question));
            var pollVotes = pollVoteIds.Length == 0
                ? new Dictionary<Guid, (string? Option, string? Custom)>()
                : (await _context.PollVotes.AsNoTracking()
                        .Where(vote => pollVoteIds.Contains(vote.Id))
                        .Select(vote => new
                        {
                            vote.Id,
                            Option = vote.PollOption != null ? vote.PollOption.Text : null,
                            vote.CustomAnswer
                        })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(
                        vote => vote.Id,
                        vote => (Option: vote.Option, Custom: vote.CustomAnswer));
            var checkVotes = checkVoteIds.Length == 0
                ? new Dictionary<Guid, (string? Option, string? Custom)>()
                : (await _context.UserCheckVotes.AsNoTracking()
                        .Where(vote => checkVoteIds.Contains(vote.Id))
                        .Select(vote => new
                        {
                            vote.Id,
                            Option = vote.CheckOption != null ? vote.CheckOption.Text : null,
                            vote.CustomAnswer
                        })
                        .ToListAsync(cancellationToken))
                    .ToDictionary(
                        vote => vote.Id,
                        vote => (Option: vote.Option, Custom: vote.CustomAnswer));

            foreach (var row in rows)
            {
                var context = new AskBackContextResponse
                {
                    ContentType = row.ContentType == ShareContentType.Poll ? "poll" : "check",
                    ContentId = row.ContentId
                };
                if (row.ContentType == ShareContentType.Poll)
                {
                    context.Title = "Question";
                    pollQuestions.TryGetValue(row.ContentId, out var question);
                    context.Question = question;
                    if (pollVotes.TryGetValue(row.VoteId, out var vote))
                    {
                        context.SelectedOption = vote.Option ?? vote.Custom;
                    }
                }
                else
                {
                    if (checkMeta.TryGetValue(row.ContentId, out var meta))
                    {
                        context.Title = meta.Title;
                        context.Question = meta.Question;
                    }

                    if (checkVotes.TryGetValue(row.VoteId, out var vote))
                    {
                        context.SelectedOption = vote.Option ?? vote.Custom;
                    }
                }

                result[row.Id] = context;
            }

            return result;
        }

        public async Task<AskBackPage<AskBackMessageResponse>?> ListMessagesAsync(
            Guid conversationId,
            Guid userId,
            (DateTime Timestamp, Guid Id)? cursor,
            bool newer,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            if (!await IsParticipantAsync(conversationId, userId, cancellationToken))
            {
                return null;
            }

            var query = _context.AskBackMessages
                .AsNoTracking()
                .Where(message => message.ConversationId == conversationId && message.DeletedAt == null);

            List<AskBackMessage> rows;
            if (newer)
            {
                var position = cursor!.Value;
                rows = await query
                    .Where(message => message.CreatedAt > position.Timestamp
                                      || (message.CreatedAt == position.Timestamp && message.Id.CompareTo(position.Id) > 0))
                    .OrderBy(message => message.CreatedAt)
                    .ThenBy(message => message.Id)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                if (cursor is { } position)
                {
                    query = query.Where(message => message.CreatedAt < position.Timestamp
                                                   || (message.CreatedAt == position.Timestamp && message.Id.CompareTo(position.Id) < 0));
                }

                rows = await query
                    .OrderByDescending(message => message.CreatedAt)
                    .ThenByDescending(message => message.Id)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);
            }

            var hasMore = rows.Count > pageSize;
            rows = rows.Take(pageSize).ToList();
            if (!newer)
            {
                rows.Reverse();
            }

            var senderIds = rows
                .Where(message => message.SenderUserId.HasValue)
                .Select(message => message.SenderUserId!.Value)
                .Distinct()
                .ToArray();
            var names = await LoadNamesAsync(senderIds, cancellationToken);

            var items = rows
                .Select(message => new AskBackMessageResponse
                {
                    Id = message.Id,
                    ConversationId = message.ConversationId,
                    ClientMessageId = message.ClientMessageId,
                    Content = message.Content,
                    SenderName = message.SenderUserId is { } sender && names.TryGetValue(sender, out var name)
                        ? name
                        : AskBackOptions.DeletedUserName,
                    IsMine = message.SenderUserId == userId,
                    CreatedAt = message.CreatedAt,
                    Cursor = AskBackCursor.Encode(message.CreatedAt, message.Id)
                })
                .ToList();

            // Older: continue before the oldest message of the page. Newer: continue after the newest.
            string? next = null;
            if (hasMore && items.Count > 0)
            {
                next = newer ? items[^1].Cursor : items[0].Cursor;
            }

            return new AskBackPage<AskBackMessageResponse>
            {
                Items = items,
                HasMore = hasMore,
                NextCursor = next
            };
        }

        public async Task<AskBackMessageResponse?> FindByClientMessageIdAsync(
            Guid conversationId,
            Guid senderUserId,
            Guid clientMessageId,
            CancellationToken cancellationToken = default)
        {
            var message = await _context.AskBackMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.ConversationId == conversationId
                            && item.SenderUserId == senderUserId
                            && item.ClientMessageId == clientMessageId,
                    cancellationToken);
            if (message is null)
            {
                return null;
            }

            var name = await GetAnonymousNameAsync(senderUserId, cancellationToken) ?? AskBackOptions.DeletedUserName;
            return new AskBackMessageResponse
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                ClientMessageId = message.ClientMessageId,
                Content = message.Content,
                SenderName = name,
                IsMine = true,
                CreatedAt = message.CreatedAt,
                Cursor = AskBackCursor.Encode(message.CreatedAt, message.Id)
            };
        }

        public Task<AskBackMessage?> FindMessageAsync(
            Guid conversationId,
            Guid messageId,
            CancellationToken cancellationToken = default)
        {
            return _context.AskBackMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    message => message.Id == messageId
                               && message.ConversationId == conversationId
                               && message.DeletedAt == null,
                    cancellationToken);
        }

        public async Task<(Guid MessageId, bool Created)> AddMessageAsync(
            AskBackConversation conversation,
            AskBackMessage message,
            Notification? notification,
            AskBackOutboxEvent? outbox,
            AskBackMessageDeliveryTracking? delivery,
            CancellationToken cancellationToken = default)
        {
            // Message, inbox row, outbox, delivery tracking and the conversation's last-message time are one
            // SaveChanges, so the tracking row exists before any delivery is attempted.
            _context.AskBackMessages.Add(message);
            if (notification is not null)
            {
                _context.Notifications.Add(notification);
            }

            if (outbox is not null)
            {
                _context.AskBackOutbox.Add(outbox);
            }

            if (delivery is not null)
            {
                _context.AskBackDeliveries.Add(delivery);
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return (message.Id, true);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, IdempotencyConstraint))
            {
                // The same client message id was saved by a concurrent retry.
                _context.ChangeTracker.Clear();
                var existing = await _context.AskBackMessages
                    .AsNoTracking()
                    .Where(item => item.ConversationId == message.ConversationId
                                   && item.SenderUserId == message.SenderUserId
                                   && item.ClientMessageId == message.ClientMessageId)
                    .Select(item => item.Id)
                    .FirstAsync(cancellationToken);
                return (existing, false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // The message counters moved under us: another send by this person (or the other person's reply)
                // landed first, so by the turn rule this one no longer fits.
                _context.ChangeTracker.Clear();

                // A concurrent retry of this very message is not a second send: hand back the stored one.
                var stored = await _context.AskBackMessages
                    .AsNoTracking()
                    .Where(item => item.ConversationId == message.ConversationId
                                   && item.SenderUserId == message.SenderUserId
                                   && item.ClientMessageId == message.ClientMessageId)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                if (stored is { } storedId)
                {
                    return (storedId, false);
                }

                throw new AppException(
                    409,
                    ApiErrorCodes.AskBackNotYourTurn,
                    "It is not your turn to send a message.");
            }
        }

        public async Task<bool> MarkDeliveryAttemptAsync(
            Guid messageId,
            Guid recipientUserId,
            AskBackDeliveryStatus status,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var updated = await _context.AskBackDeliveries
                .Where(delivery => delivery.MessageId == messageId
                                   && delivery.RecipientUserId == recipientUserId
                                   && delivery.Status == AskBackDeliveryStatus.Pending)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(delivery => delivery.Status, status)
                        .SetProperty(delivery => delivery.SentAt, (DateTime?)utcNow)
                        .SetProperty(delivery => delivery.UpdatedAt, utcNow),
                    cancellationToken);
            return updated > 0;
        }

        public async Task<bool> MarkDeliveryFailedAsync(
            Guid messageId,
            Guid recipientUserId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var updated = await _context.AskBackDeliveries
                .Where(delivery => delivery.MessageId == messageId
                                   && delivery.RecipientUserId == recipientUserId
                                   && (delivery.Status == AskBackDeliveryStatus.Pending
                                       || delivery.Status == AskBackDeliveryStatus.Sent))
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(delivery => delivery.Status, AskBackDeliveryStatus.Failed)
                        .SetProperty(delivery => delivery.UpdatedAt, utcNow),
                    cancellationToken);
            return updated > 0;
        }

        public async Task<AskBackAckResult> AcknowledgeDeliveryAsync(
            Guid messageId,
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            // Any state but Delivered can still be acknowledged: a timed-out or failed send may have arrived anyway.
            // Of two concurrent updates, PostgreSQL re-checks the second against the first's result, so it matches
            // nothing.
            var updated = await _context.AskBackDeliveries
                .Where(delivery => delivery.MessageId == messageId
                                   && delivery.RecipientUserId == userId
                                   && delivery.Status != AskBackDeliveryStatus.Delivered)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(delivery => delivery.Status, AskBackDeliveryStatus.Delivered)
                        .SetProperty(delivery => delivery.DeliveredAt, (DateTime?)utcNow)
                        .SetProperty(delivery => delivery.UpdatedAt, utcNow),
                    cancellationToken);

            var row = await _context.AskBackDeliveries
                .AsNoTracking()
                .Where(delivery => delivery.MessageId == messageId && delivery.RecipientUserId == userId)
                .Select(delivery => new
                {
                    delivery.Status,
                    delivery.SentAt,
                    delivery.DeliveredAt,
                    delivery.Message.ConversationId
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (row is null)
            {
                return new AskBackAckResult(AskBackAckOutcome.NotFound, null, null, null);
            }

            var outcome = updated > 0 ? AskBackAckOutcome.Delivered : AskBackAckOutcome.AlreadyDelivered;
            return new AskBackAckResult(outcome, row.ConversationId, row.SentAt, row.DeliveredAt);
        }

        public async Task<int> CountUnreadAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var read = await _context.AskBackParticipants
                .AsNoTracking()
                .Where(participant => participant.ConversationId == conversationId && participant.UserId == userId)
                .Select(participant => new { participant.LastReadMessageCreatedAt, participant.LastReadMessageId })
                .FirstOrDefaultAsync(cancellationToken);
            if (read is null)
            {
                return 0;
            }

            var query = _context.AskBackMessages
                .AsNoTracking()
                .Where(message => message.ConversationId == conversationId
                                  && message.DeletedAt == null
                                  && message.SenderUserId != userId);
            if (read.LastReadMessageCreatedAt is { } readAt && read.LastReadMessageId is { } readId)
            {
                query = query.Where(message => message.CreatedAt > readAt
                                               || (message.CreatedAt == readAt && message.Id.CompareTo(readId) > 0));
            }

            return await query.CountAsync(cancellationToken);
        }

        public async Task<bool> IsTopAskableReasonAsync(
            AskBackVoteContext vote,
            CancellationToken cancellationToken = default)
        {
            if (vote.ContentType == ShareContentType.Poll)
            {
                var own = await _context.PollVotes
                    .AsNoTracking()
                    .Where(item => item.Id == vote.VoteId)
                    .Select(item => new { item.PollOptionId })
                    .FirstOrDefaultAsync(cancellationToken);
                if (own is null)
                {
                    return false;
                }

                // A custom answer is its own option, so its reason is always the top one.
                if (own.PollOptionId is null)
                {
                    return true;
                }

                var topId = await _context.PollVotes
                    .AsNoTracking()
                    .Where(item => item.PollId == vote.ContentId
                                   && item.VoterId != vote.OwnerId
                                   && item.PollOptionId == own.PollOptionId
                                   && item.Reason != null
                                   && item.VoterRole == VoterRole.User)
                    .OrderByDescending(item => item.CreatedAt)
                    .ThenByDescending(item => item.Id)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken);
                return topId == vote.VoteId;
            }

            var checkVote = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(item => item.Id == vote.VoteId)
                .Select(item => new { item.CheckOptionId })
                .FirstOrDefaultAsync(cancellationToken);
            if (checkVote is null)
            {
                return false;
            }

            if (checkVote.CheckOptionId is null)
            {
                return true;
            }

            var topCheckId = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(item => item.UserCheckId == vote.ContentId
                               && item.VoterId != vote.OwnerId
                               && item.CheckOptionId == checkVote.CheckOptionId
                               && item.Reason != null
                               && item.VoterRole == VoterRole.User)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Select(item => (Guid?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);
            return topCheckId == vote.VoteId;
        }

        public Task<bool> HasConversationOnCardAsync(
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .AsNoTracking()
                .AnyAsync(
                    conversation => conversation.ContentType == contentType && conversation.ContentId == contentId,
                    cancellationToken);
        }

        public async Task<int> CountReasonsOnCardAsync(
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            if (contentType == ShareContentType.Poll)
            {
                var ownerId = await _context.Polls.AsNoTracking()
                    .Where(poll => poll.Id == contentId)
                    .Select(poll => (Guid?)poll.CreatorId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (ownerId is null)
                {
                    return 0;
                }

                return await _context.PollVotes.AsNoTracking()
                    .CountAsync(
                        vote => vote.PollId == contentId
                                && vote.VoterId != ownerId
                                && vote.Reason != null,
                        cancellationToken);
            }

            var checkOwnerId = await _context.UserChecks.AsNoTracking()
                .Where(item => item.Id == contentId)
                .Select(item => (Guid?)item.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (checkOwnerId is null)
            {
                return 0;
            }

            return await _context.UserCheckVotes.AsNoTracking()
                .CountAsync(
                    vote => vote.UserCheckId == contentId
                            && vote.VoterId != checkOwnerId
                            && vote.Reason != null,
                    cancellationToken);
        }

        public async Task<(FreeAskOutcome Outcome, Guid? ConversationId)> AddFreeAskConversationAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // One lock per card, held until the transaction ends: the check below and the insert are then one step.
            var lockKey = $"ask-back-free:{(int)conversation.ContentType}:{conversation.ContentId}";
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);

            var existingForVote = await FindConversationIdByVoteAsync(conversation.VoteId, cancellationToken);
            if (existingForVote is { } found)
            {
                return (FreeAskOutcome.AlreadyExists, found);
            }

            if (await HasConversationOnCardAsync(conversation.ContentType, conversation.ContentId, cancellationToken))
            {
                return (FreeAskOutcome.CardAlreadyUsed, null);
            }

            _context.AskBackConversations.Add(conversation);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (FreeAskOutcome.Created, conversation.Id);
        }

        public Task<bool> IsBlockedEitherWayAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default)
        {
            return _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(
                    block => (block.BlockerUserId == userA && block.BlockedUserId == userB)
                             || (block.BlockerUserId == userB && block.BlockedUserId == userA),
                    cancellationToken);
        }

        public async Task AddUserBlockAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            Guid sourceConversationId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var exists = await _context.UserBlocks.AnyAsync(
                block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId,
                cancellationToken);
            if (!exists)
            {
                _context.UserBlocks.Add(UserBlock.Create(blockerUserId, blockedUserId, sourceConversationId, utcNow));
            }
        }

        public async Task RemoveUserBlockAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.UserBlocks
                .Where(block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId)
                .ToListAsync(cancellationToken);
            _context.UserBlocks.RemoveRange(existing);
        }

        public async Task<(IReadOnlyList<UserBlock> Items, int Total)> ListBlocksForBlockerAsync(
            Guid blockerUserId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.UserBlocks
                .AsNoTracking()
                .Where(block => block.BlockerUserId == blockerUserId);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(block => block.CreatedAt)
                .ThenByDescending(block => block.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, total);
        }

        public Task<UserBlock?> FindOwnedBlockAsync(
            Guid blockerUserId,
            Guid blockId,
            CancellationToken cancellationToken = default)
        {
            return _context.UserBlocks.FirstOrDefaultAsync(
                block => block.Id == blockId && block.BlockerUserId == blockerUserId,
                cancellationToken);
        }

        public async Task<IReadOnlyList<AskBackConversation>> ListBlockedConversationsByPairAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default)
        {
            return await _context.AskBackConversations
                .Where(conversation =>
                    conversation.Status == AskBackStatus.Blocked
                    && conversation.BlockedByUserId == blockerUserId
                    && (
                        (conversation.CreatorUserId == blockerUserId && conversation.VoterUserId == blockedUserId)
                        || (conversation.CreatorUserId == blockedUserId && conversation.VoterUserId == blockerUserId)))
                .ToListAsync(cancellationToken);
        }

        public void AddReport(AskBackReport report)
        {
            _context.AskBackReports.Add(report);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<Dictionary<Guid, string>> LoadNamesAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken)
        {
            if (userIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var ids = userIds.ToArray();
            return await _context.Users
                .AsNoTracking()
                .Where(user => ids.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.AnonymousName, cancellationToken);
        }

        private async Task<AskBackContextResponse> LoadContextAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken)
        {
            var context = new AskBackContextResponse
            {
                ContentType = conversation.ContentType == ShareContentType.Poll ? "poll" : "check",
                ContentId = conversation.ContentId
            };

            // Read live from the vote so a removed (reported) reason disappears here too. Gone content stays null.
            if (conversation.ContentType == ShareContentType.Poll)
            {
                context.Title = "Question";
                context.Question = await _context.Polls
                    .AsNoTracking()
                    .Where(poll => poll.Id == conversation.ContentId)
                    .Select(poll => poll.Question)
                    .FirstOrDefaultAsync(cancellationToken);
                var vote = await _context.PollVotes
                    .AsNoTracking()
                    .Where(item => item.Id == conversation.VoteId)
                    .Select(item => new { Option = item.PollOption != null ? item.PollOption.Text : null, item.CustomAnswer, item.Reason })
                    .FirstOrDefaultAsync(cancellationToken);
                context.SelectedOption = vote?.Option ?? vote?.CustomAnswer;
                context.Reason = vote?.Reason;
            }
            else
            {
                var check = await _context.UserChecks
                    .AsNoTracking()
                    .Where(item => item.Id == conversation.ContentId)
                    .Select(item => new { item.Check.Title, item.Check.Question })
                    .FirstOrDefaultAsync(cancellationToken);
                context.Title = check?.Title;
                context.Question = check?.Question;
                var vote = await _context.UserCheckVotes
                    .AsNoTracking()
                    .Where(item => item.Id == conversation.VoteId)
                    .Select(item => new { Option = item.CheckOption != null ? item.CheckOption.Text : null, item.CustomAnswer, item.Reason })
                    .FirstOrDefaultAsync(cancellationToken);
                context.SelectedOption = vote?.Option ?? vote?.CustomAnswer;
                context.Reason = vote?.Reason;
            }

            return context;
        }

        private static AskBackParticipantResponse ToParticipant(Guid? userId, Dictionary<Guid, string> names)
        {
            if (userId is { } id && names.TryGetValue(id, out var name))
            {
                return new AskBackParticipantResponse { AnonymousName = name };
            }

            return new AskBackParticipantResponse { AnonymousName = AskBackOptions.DeletedUserName, IsDeleted = true };
        }

        private static string StatusName(AskBackStatus status)
        {
            return status.ToString().ToLowerInvariant();
        }

        private static string Preview(string content)
        {
            var text = content.ReplaceLineEndings(" ");
            return text.Length <= AskBackOptions.PreviewLength ? text : text[..AskBackOptions.PreviewLength] + "…";
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres
                   && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                   && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
