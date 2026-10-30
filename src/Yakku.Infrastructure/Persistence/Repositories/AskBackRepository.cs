using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.AskBack;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
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
                               && user.DeletionScheduledFor == null)
                .Select(user => user.Id)
                .ToListAsync(cancellationToken);
            return usable.ToHashSet();
        }

        public Task<string?> GetAnonymousNameAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.UserProfiles
                .AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => (string?)profile.AnonymousName)
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

        public Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.AskBackConversations
                .AsNoTracking()
                .AnyAsync(
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
            var mine = conversation.Participants.First(participant => participant.UserId == userId);

            return new AskBackConversationResponse
            {
                Id = conversation.Id,
                Status = StatusName(conversation.Status),
                MyRole = isAsker ? "asker" : "voter",
                Other = ToParticipant(otherId, names),
                Context = await LoadContextAsync(conversation, cancellationToken),
                CreatedAt = conversation.CreatedAt,
                ClosedAt = conversation.ClosedAt,
                IsMuted = mine.IsMuted,
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

            var muted = (await _context.AskBackParticipants
                    .AsNoTracking()
                    .Where(participant => participant.UserId == userId && ids.Contains(participant.ConversationId))
                    .Select(participant => new { participant.ConversationId, participant.IsMuted })
                    .ToListAsync(cancellationToken))
                .ToDictionary(item => item.ConversationId, item => item.IsMuted);

            var items = rows.Select(row =>
            {
                lastMessages.TryGetValue(row.Id, out var last);
                return new AskBackConversationListItemResponse
                {
                    Id = row.Id,
                    Status = StatusName(row.Status),
                    MyRole = row.CreatorUserId == userId ? "asker" : "voter",
                    Other = ToParticipant(row.OtherParticipant(userId), names),
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
                    IsMuted = muted.GetValueOrDefault(row.Id),
                    CreatedAt = row.CreatedAt
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
            AskBackOutboxEvent? outbox,
            CancellationToken cancellationToken = default)
        {
            // Message, outbox row and the conversation's last-message time are one SaveChanges: one transaction.
            _context.AskBackMessages.Add(message);
            if (outbox is not null)
            {
                _context.AskBackOutbox.Add(outbox);
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
            return await _context.UserProfiles
                .AsNoTracking()
                .Where(profile => ids.Contains(profile.UserId))
                .ToDictionaryAsync(profile => profile.UserId, profile => profile.AnonymousName, cancellationToken);
        }

        private async Task<AskBackContextResponse> LoadContextAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken)
        {
            var context = new AskBackContextResponse
            {
                ContentType = conversation.ContentType == ShareContentType.Poll ? "poll" : "check"
            };

            // Read live from the vote so a removed (reported) reason disappears here too. Gone content stays null.
            if (conversation.ContentType == ShareContentType.Poll)
            {
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
                context.Question = await _context.UserChecks
                    .AsNoTracking()
                    .Where(item => item.Id == conversation.ContentId)
                    .Select(item => item.Check.Question)
                    .FirstOrDefaultAsync(cancellationToken);
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
