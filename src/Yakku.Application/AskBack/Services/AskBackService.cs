using FluentValidation;
using Microsoft.Extensions.Logging;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AskBack.Services
{
    /// <summary>
    /// Ask Back business rules. Identities come from the database and the caller's token, never from the request.
    /// A user who is not a participant of a conversation gets a 404, the same as for one that does not exist, so
    /// conversation ids reveal nothing.
    /// </summary>
    public class AskBackService : IAskBackService
    {
        private readonly IAskBackRepository _repository;
        private readonly IAskBackRealtime _realtime;
        private readonly IValidator<CreateAskBackConversationRequest> _createValidator;
        private readonly IValidator<SendAskBackMessageRequest> _sendValidator;
        private readonly IValidator<MarkAskBackReadRequest> _readValidator;
        private readonly IValidator<BlockAskBackRequest> _blockValidator;
        private readonly TimeProvider _time;
        private readonly ILogger<AskBackService> _logger;

        public AskBackService(
            IAskBackRepository repository,
            IAskBackRealtime realtime,
            IValidator<CreateAskBackConversationRequest> createValidator,
            IValidator<SendAskBackMessageRequest> sendValidator,
            IValidator<MarkAskBackReadRequest> readValidator,
            IValidator<BlockAskBackRequest> blockValidator,
            TimeProvider time,
            ILogger<AskBackService> logger)
        {
            _repository = repository;
            _realtime = realtime;
            _createValidator = createValidator;
            _sendValidator = sendValidator;
            _readValidator = readValidator;
            _blockValidator = blockValidator;
            _time = time;
            _logger = logger;
        }

        public async Task<(AskBackConversationResponse Conversation, bool Created)> CreateConversationAsync(
            Guid userId,
            CreateAskBackConversationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

            var vote = await _repository.FindVoteContextAsync(request.VoteId, cancellationToken);

            // Not the owner is a 404, like the reasons list: a vote id says nothing to anyone but the owner.
            if (vote is null || vote.OwnerId != userId)
            {
                throw NotFound("Vote not found.");
            }

            if (vote.VoterId == userId)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "You cannot ask yourself back.",
                    "voteId");
            }

            // Reasons are revealed only once enough other people answered; so is Ask Back.
            if (vote.OtherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                throw NotFound("Vote not found.");
            }

            // The caller owns the vote, so is the asker of its conversation: return it as it is.
            var existingId = await _repository.FindConversationIdByVoteAsync(vote.VoteId, cancellationToken);
            if (existingId is { } found)
            {
                var existing = await _repository.GetDetailsAsync(found, userId, cancellationToken)
                    ?? throw NotFound("Conversation not found.");
                return (existing, false);
            }

            if (vote.VoterRole != VoterRole.User)
            {
                throw NotAllowed("This voter cannot be asked back.");
            }

            if (!vote.HasReason)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "This vote has no reason to ask about.",
                    "voteId");
            }

            var usable = await _repository.GetUsableUserIdsAsync([userId, vote.VoterId], cancellationToken);
            if (!usable.Contains(userId) || !usable.Contains(vote.VoterId))
            {
                throw NotAllowed("This voter cannot be asked back.");
            }

            var conversation = AskBackConversation.Create(
                vote.ContentType,
                vote.ContentId,
                vote.VoteId,
                userId,
                vote.VoterId,
                _time.GetUtcNow().UtcDateTime);

            var (conversationId, created) = await _repository.AddOrGetAsync(conversation, cancellationToken);
            if (created)
            {
                _logger.LogInformation("Ask Back conversation {ConversationId} created.", conversationId);
            }

            var details = await _repository.GetDetailsAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
            return (details, created);
        }

        public Task<AskBackPage<AskBackConversationListItemResponse>> ListConversationsAsync(
            Guid userId,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken = default)
        {
            return _repository.ListAsync(
                userId,
                AskBackCursor.TryDecode(cursor),
                ClampPageSize(limit, AskBackOptions.ConversationPageSize),
                cancellationToken);
        }

        public async Task<AskBackConversationResponse> GetConversationAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetDetailsAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
        }

        public async Task<AskBackPage<AskBackMessageResponse>> ListMessagesAsync(
            Guid userId,
            Guid conversationId,
            string? cursor,
            string? direction,
            int? limit,
            CancellationToken cancellationToken = default)
        {
            var newer = ParseDirection(direction);
            if (newer && string.IsNullOrWhiteSpace(cursor))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "A cursor is required to load newer messages.",
                    "cursor");
            }

            return await _repository.ListMessagesAsync(
                    conversationId,
                    userId,
                    AskBackCursor.TryDecode(cursor),
                    newer,
                    ClampPageSize(limit, AskBackOptions.MessagePageSize),
                    cancellationToken)
                ?? throw NotFound("Conversation not found.");
        }

        public async Task<AskBackMessageResponse> SendMessageAsync(
            Guid userId,
            Guid conversationId,
            SendAskBackMessageRequest request,
            CancellationToken cancellationToken = default)
        {
            await _sendValidator.ValidateAndThrowAsync(request, cancellationToken);

            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            // A retry of a message that was saved must succeed even if the conversation has been closed since.
            var existing = await _repository.FindByClientMessageIdAsync(
                conversationId, userId, request.ClientMessageId, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            if (!conversation.CanSend)
            {
                throw NotActive(conversation);
            }

            var usable = await _repository.GetUsableUserIdsAsync([userId], cancellationToken);
            if (!usable.Contains(userId))
            {
                throw NotAllowed("Your account cannot send messages.");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var message = AskBackMessage.Create(
                conversationId, userId, request.ClientMessageId, request.Content.Trim(), now);
            conversation.RecordMessage(now);

            var recipientId = conversation.OtherParticipant(userId);
            var recipientMuted = conversation.Participants.Any(p => p.UserId == recipientId && p.IsMuted);
            var outbox = recipientId is { } recipient && !recipientMuted
                ? AskBackOutboxEvent.ForMessage(conversationId, message.Id, recipient, now, AskBackOptions.PushDelay)
                : null;

            var (messageId, created) = await _repository.AddMessageAsync(conversation, message, outbox, cancellationToken);
            if (!created)
            {
                return await _repository.FindByClientMessageIdAsync(
                        conversationId, userId, request.ClientMessageId, cancellationToken)
                    ?? throw NotFound("Message not found.");
            }

            var senderName = await _repository.GetAnonymousNameAsync(userId, cancellationToken)
                ?? AskBackOptions.DeletedUserName;
            var forSender = ToResponse(message, senderName, isMine: true);
            var forRecipient = ToResponse(message, senderName, isMine: false);

            // The message is saved; delivery is best effort. A client that misses this reads it from the history.
            await TryPublishAsync(
                () => _realtime.MessageCreatedAsync(
                    conversationId, userId, recipientId, forSender, forRecipient, cancellationToken),
                "message");

            return forSender;
        }

        public async Task<AskBackReadResponse> MarkReadAsync(
            Guid userId,
            Guid conversationId,
            MarkAskBackReadRequest request,
            CancellationToken cancellationToken = default)
        {
            await _readValidator.ValidateAndThrowAsync(request, cancellationToken);

            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
            var message = await _repository.FindMessageAsync(conversationId, request.LastReadMessageId, cancellationToken)
                ?? throw NotFound("Message not found.");
            var participant = conversation.Participants.First(p => p.UserId == userId);

            var now = _time.GetUtcNow().UtcDateTime;
            var advanced = participant.TryAdvanceRead(message.Id, message.CreatedAt, now);
            if (advanced)
            {
                await _repository.SaveAsync(cancellationToken);
                await TryPublishAsync(
                    () => _realtime.MessageReadAsync(
                        conversationId, conversation.OtherParticipant(userId), message.Id, now, cancellationToken),
                    "read");
            }

            return new AskBackReadResponse
            {
                LastReadMessageId = participant.LastReadMessageId ?? message.Id,
                Advanced = advanced,
                UnreadCount = await _repository.CountUnreadAsync(conversationId, userId, cancellationToken)
            };
        }

        public async Task<AskBackConversationResponse> SetMutedAsync(
            Guid userId,
            Guid conversationId,
            bool muted,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
            conversation.Participants.First(p => p.UserId == userId).SetMuted(muted);
            await _repository.SaveAsync(cancellationToken);
            return await GetConversationAsync(userId, conversationId, cancellationToken);
        }

        public async Task<AskBackConversationResponse> CloseAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            if (conversation.Status == AskBackStatus.Blocked)
            {
                throw NotActive(conversation);
            }

            if (conversation.TryClose(_time.GetUtcNow().UtcDateTime))
            {
                await _repository.SaveAsync(cancellationToken);
                await PublishStatusAsync(conversation, cancellationToken);
            }

            return await GetConversationAsync(userId, conversationId, cancellationToken);
        }

        public async Task<AskBackConversationResponse> ReopenAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            if (conversation.Status == AskBackStatus.Active)
            {
                return await GetConversationAsync(userId, conversationId, cancellationToken);
            }

            if (conversation.Status == AskBackStatus.Blocked)
            {
                throw NotActive(conversation);
            }

            if (!conversation.TryReopen(userId))
            {
                throw NotAllowed("Only the person who asked can reopen this conversation.");
            }

            await _repository.SaveAsync(cancellationToken);
            await PublishStatusAsync(conversation, cancellationToken);
            return await GetConversationAsync(userId, conversationId, cancellationToken);
        }

        public async Task<AskBackConversationResponse> BlockAsync(
            Guid userId,
            Guid conversationId,
            BlockAskBackRequest request,
            CancellationToken cancellationToken = default)
        {
            await _blockValidator.ValidateAndThrowAsync(request, cancellationToken);

            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            var now = _time.GetUtcNow().UtcDateTime;
            if (conversation.TryBlock(userId, now))
            {
                if (!string.IsNullOrWhiteSpace(request.Reason))
                {
                    _repository.AddReport(AskBackReport.Create(
                        conversationId, userId, conversation.OtherParticipant(userId), request.Reason.Trim(), now));
                }

                await _repository.SaveAsync(cancellationToken);
                await PublishStatusAsync(conversation, cancellationToken);
            }

            return await GetConversationAsync(userId, conversationId, cancellationToken);
        }

        public async Task<AskBackConversationResponse> UnblockAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            var conversation = await _repository.FindForParticipantAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            if (conversation.Status != AskBackStatus.Blocked)
            {
                return await GetConversationAsync(userId, conversationId, cancellationToken);
            }

            if (!conversation.TryUnblock(userId))
            {
                throw NotAllowed("Only the person who blocked can lift the block.");
            }

            await _repository.SaveAsync(cancellationToken);
            await PublishStatusAsync(conversation, cancellationToken);
            return await GetConversationAsync(userId, conversationId, cancellationToken);
        }

        public async Task<bool> CanJoinAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty || conversationId == Guid.Empty)
            {
                return false;
            }

            if (!await _repository.IsParticipantAsync(conversationId, userId, cancellationToken))
            {
                return false;
            }

            var usable = await _repository.GetUsableUserIdsAsync([userId], cancellationToken);
            return usable.Contains(userId);
        }

        private Task PublishStatusAsync(AskBackConversation conversation, CancellationToken cancellationToken)
        {
            var userIds = new[] { conversation.CreatorUserId, conversation.VoterUserId }
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToArray();
            return TryPublishAsync(
                () => _realtime.StatusChangedAsync(
                    conversation.Id, userIds, conversation.Status.ToString().ToLowerInvariant(), cancellationToken),
                "status");
        }

        private async Task TryPublishAsync(Func<Task> publish, string what)
        {
            try
            {
                await publish();
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Ask Back real-time {Event} delivery failed.", what);
            }
        }

        private static AskBackMessageResponse ToResponse(AskBackMessage message, string senderName, bool isMine)
        {
            return new AskBackMessageResponse
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                ClientMessageId = message.ClientMessageId,
                Content = message.Content,
                SenderName = senderName,
                IsMine = isMine,
                CreatedAt = message.CreatedAt,
                Cursor = AskBackCursor.Encode(message.CreatedAt, message.Id)
            };
        }

        private static bool ParseDirection(string? direction)
        {
            if (string.IsNullOrWhiteSpace(direction) || direction.Equals("older", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (direction.Equals("newer", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            throw new AppException(
                400,
                ApiErrorCodes.ValidationError,
                "Direction must be older or newer.",
                "direction");
        }

        private static int ClampPageSize(int? requested, int defaultSize)
        {
            return requested is null or <= 0 ? defaultSize : Math.Min(requested.Value, AskBackOptions.MaxPageSize);
        }

        private static AppException NotFound(string message)
        {
            return new AppException(404, ApiErrorCodes.NotFound, message);
        }

        private static AppException NotAllowed(string message)
        {
            return new AppException(403, ApiErrorCodes.AskBackNotAllowed, message);
        }

        private static AppException NotActive(AskBackConversation conversation)
        {
            var message = conversation.Status switch
            {
                AskBackStatus.Blocked => "This conversation is blocked.",
                AskBackStatus.Closed => "This conversation is closed.",
                _ => "The other person is no longer on Yakku."
            };
            return new AppException(409, ApiErrorCodes.AskBackNotActive, message);
        }
    }
}
