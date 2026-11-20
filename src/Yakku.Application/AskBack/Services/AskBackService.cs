using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Entitlements;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.PushNotifications;
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
        private readonly INotificationRepository _notifications;
        private readonly IValidator<CreateAskBackConversationRequest> _createValidator;
        private readonly IValidator<SendAskBackMessageRequest> _sendValidator;
        private readonly IValidator<MarkAskBackReadRequest> _readValidator;
        private readonly IValidator<BlockAskBackRequest> _blockValidator;
        private readonly IEntitlementService _entitlements;
        private readonly TimeProvider _time;
        private readonly ILogger<AskBackService> _logger;

        public AskBackService(
            IAskBackRepository repository,
            IAskBackRealtime realtime,
            INotificationRepository notifications,
            IValidator<CreateAskBackConversationRequest> createValidator,
            IValidator<SendAskBackMessageRequest> sendValidator,
            IValidator<MarkAskBackReadRequest> readValidator,
            IValidator<BlockAskBackRequest> blockValidator,
            IEntitlementService entitlements,
            TimeProvider time,
            ILogger<AskBackService> logger)
        {
            _entitlements = entitlements;
            _repository = repository;
            _realtime = realtime;
            _notifications = notifications;
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
                return (await LoadDetailsAsync(found, userId, cancellationToken), false);
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

            // The voter decides whether the people they answered may start a conversation.
            if (!await _repository.AcceptsAskBacksAsync(vote.VoterId, cancellationToken))
            {
                throw NotAllowed("This voter cannot be asked back.");
            }

            // Someone who blocked the other (either way) cannot start a new conversation with them, and is not told why.
            if (await _repository.IsBlockedEitherWayAsync(userId, vote.VoterId, cancellationToken))
            {
                throw NotAllowed("This voter cannot be asked back.");
            }

            // Who may ask back: a card unlocked by a plan ticket (or automatically by an unlimited plan) or on its
            // own, or, once per card, the top reason of any option for free.
            var access = await _entitlements.GetCardAccessAsync(
                userId, vote.ContentType, vote.ContentId, cancellationToken);
            var useFreeAskBack = false;
            if (!access.Unlocked)
            {
                if (access.CanAutoUnlock)
                {
                    if (!await _entitlements.TryAutoUnlockAsync(
                            userId, vote.ContentType, vote.ContentId, cancellationToken))
                    {
                        throw Locked();
                    }
                }
                else if (await _repository.IsTopAskableReasonAsync(vote, cancellationToken))
                {
                    useFreeAskBack = true;
                }
                else
                {
                    throw Locked();
                }
            }
            else if (access.Paused)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.AskBackPlanExpired,
                    "Your plan has expired. Choose a plan to keep asking back on this card.");
            }

            var conversation = AskBackConversation.Create(
                vote.ContentType,
                vote.ContentId,
                vote.VoteId,
                userId,
                vote.VoterId,
                _time.GetUtcNow().UtcDateTime);

            Guid conversationId;
            bool created;
            if (useFreeAskBack)
            {
                var (outcome, freeId) = await _repository.AddFreeAskConversationAsync(conversation, cancellationToken);
                if (outcome == FreeAskOutcome.CardAlreadyUsed)
                {
                    throw Locked();
                }

                conversationId = freeId!.Value;
                created = outcome == FreeAskOutcome.Created;
            }
            else
            {
                (conversationId, created) = await _repository.AddOrGetAsync(conversation, cancellationToken);
            }

            if (created)
            {
                _logger.LogInformation("Ask Back conversation {ConversationId} created.", conversationId);
            }

            var details = await LoadDetailsAsync(conversationId, userId, cancellationToken);
            return (details, created);
        }

        public async Task<AskBackPage<AskBackConversationListItemResponse>> ListConversationsAsync(
            Guid userId,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken = default)
        {
            var page = await _repository.ListAsync(
                userId,
                AskBackCursor.TryDecode(cursor),
                ClampPageSize(limit, AskBackOptions.ConversationPageSize),
                cancellationToken);

            // Whose turn it is depends on the asker's plan for that card; fill it in for the tabs.
            foreach (var item in page.Items)
            {
                var snapshot = await _repository.GetSnapshotAsync(item.Id, userId, cancellationToken);
                if (snapshot is null)
                {
                    continue;
                }

                var (messageLimit, paused) = await ResolveAccessAsync(snapshot, cancellationToken);
                var turn = snapshot.TurnFor(userId, messageLimit);
                item.Turn = turn.ToString().ToLowerInvariant();
                item.MessagesLeft = snapshot.MessagesLeft(userId, messageLimit);
                item.IsPaused = paused;
                item.CanSend = snapshot.CanSend && !paused && turn == AskBackTurn.Me;
            }

            return page;
        }

        public async Task<AskBackConversationResponse> GetConversationAsync(
            Guid userId,
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            return await LoadDetailsAsync(conversationId, userId, cancellationToken);
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

            // A conversation unlocked through a plan stops for both people when that plan has expired.
            var access = await ResolveAccessAsync(conversation, cancellationToken);
            if (access.Paused)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.AskBackPlanExpired,
                    "This conversation is paused.");
            }

            // The asker sends first, the voter answers once, and the asker has a limited number of questions.
            var now = _time.GetUtcNow().UtcDateTime;
            switch (conversation.TryRecordMessage(userId, access.MessageLimit, now))
            {
                case AskBackSendResult.NotYourTurn:
                    throw new AppException(
                        409,
                        ApiErrorCodes.AskBackNotYourTurn,
                        "It is not your turn to send a message.");
                case AskBackSendResult.LimitReached:
                    throw new AppException(
                        409,
                        ApiErrorCodes.AskBackLimitReached,
                        "This conversation is finished.");
            }

            var message = AskBackMessage.Create(
                conversationId, userId, request.ClientMessageId, request.Content.Trim(), now);

            var recipientId = conversation.OtherParticipant(userId);
            Notification? notification = null;
            AskBackOutboxEvent? outbox = null;

            // Always write an inbox row for the recipient. Presence only skips the push
            // (AskBackPushDispatcher); the Activity list must still show start/reply events.
            if (recipientId is { } recipient)
            {
                var senderNameForInbox = await _repository.GetAnonymousNameAsync(userId, cancellationToken)
                    ?? AskBackOptions.DeletedUserName;
                var isFirstMessage = conversation.AskerMessageCount + conversation.VoterMessageCount == 1;
                var eventType = isFirstMessage
                    ? NotificationEventType.AskBackStarted
                    : NotificationEventType.AskBackReply;
                var copy = isFirstMessage
                    ? CardActivityCopy.ForAskBackStarted(message.Content, SourceLabel(conversation.ContentType))
                    : CardActivityCopy.ForAskBackReply(message.Content);

                notification = new Notification(
                    recipient,
                    NotificationType.Normal,
                    eventType,
                    copy.Title,
                    copy.Body,
                    data: JsonSerializer.Serialize(new
                    {
                        kind = isFirstMessage ? "ask_back_started" : "ask_back_reply",
                        conversationId,
                        messageId = message.Id,
                        actorUserId = userId,
                        actorAnonymousName = senderNameForInbox,
                        avatarTone = CardActivityCopy.AvatarToneFor(userId),
                        contentType = conversation.ContentType.ToString().ToLowerInvariant(),
                        contentId = conversation.ContentId,
                        deepLink = $"yakku://ask-back/{conversationId}"
                    }));

                outbox = AskBackOutboxEvent.ForMessage(
                    conversationId,
                    message.Id,
                    recipient,
                    notification.Id,
                    now,
                    AskBackOptions.PushDelay);
            }

            var (messageId, created) = await _repository.AddMessageAsync(
                conversation, message, notification, outbox, cancellationToken);
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
                await _notifications.MarkAskBackConversationReadAsync(
                    userId, conversationId, cancellationToken);
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
                // Blocking always files a report, and the person is blocked everywhere: they can no longer vote on
                // this user's cards or ask them back. The reason is optional on the wire but the report is not.
                var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Not specified" : request.Reason.Trim();
                var other = conversation.OtherParticipant(userId);
                _repository.AddReport(AskBackReport.Create(conversationId, userId, other, reason, now));
                if (other is { } otherUserId)
                {
                    await _repository.AddUserBlockAsync(userId, otherUserId, conversationId, now, cancellationToken);
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

            if (conversation.OtherParticipant(userId) is { } blockedUserId)
            {
                await _repository.RemoveUserBlockAsync(userId, blockedUserId, cancellationToken);
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

        /// <summary>
        /// The message limit that applies to this conversation and whether it is paused. It is read live, so a plan
        /// change shows up on conversations that already exist.
        /// </summary>
        private async Task<(int? MessageLimit, bool Paused)> ResolveAccessAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken)
        {
            if (conversation.CreatorUserId is not { } askerId)
            {
                // The asker's account is gone; nobody can send anyway.
                return (await _entitlements.GetPurchaseMessageLimitAsync(cancellationToken), false);
            }

            var access = await _entitlements.GetCardAccessAsync(
                askerId, conversation.ContentType, conversation.ContentId, cancellationToken);
            return (access.MessageLimit, access.Paused);
        }

        /// <summary>The conversation as the caller sees it, with whose turn it is and how many messages are left.</summary>
        private async Task<AskBackConversationResponse> LoadDetailsAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var response = await _repository.GetDetailsAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
            var snapshot = await _repository.GetSnapshotAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            var (limit, paused) = await ResolveAccessAsync(snapshot, cancellationToken);
            var turn = snapshot.TurnFor(userId, limit);
            response.Turn = turn.ToString().ToLowerInvariant();
            response.MessagesLeft = snapshot.MessagesLeft(userId, limit);
            response.IsPaused = paused;
            response.CanSend = response.CanSend && !paused && turn == AskBackTurn.Me;
            if (turn == AskBackTurn.Done && response.MyRole == "asker")
            {
                response.WaitingReplies = await _repository.CountReasonsOnCardAsync(
                    snapshot.ContentType, snapshot.ContentId, cancellationToken);
            }

            return response;
        }

        private static AppException Locked()
        {
            return new AppException(
                403,
                ApiErrorCodes.AskBackLocked,
                "Unlock this card to ask back on this reason. Every card has one free ask back, on the top reason of an option.");
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

        private static string SourceLabel(ShareContentType contentType) =>
            contentType switch
            {
                ShareContentType.Check => "Mirror Check",
                ShareContentType.Poll => "poll",
                ShareContentType.Personality => "personality card",
                _ => contentType.ToString().ToLowerInvariant()
            };

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
