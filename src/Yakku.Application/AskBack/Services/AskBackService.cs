using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Caching;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
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
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;
        private readonly TimeProvider _time;
        private readonly ILogger<AskBackService> _logger;
        private readonly IAskBackPresence _presence;

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
            ILogger<AskBackService> logger,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null,
            IAskBackPresence? presence = null)
        {
            _entitlements = entitlements;
            _repository = repository;
            _realtime = realtime;
            _presence = presence ?? new NullAskBackPresence();
            _notifications = notifications;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
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

            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterAskBackListChange(userId), cancellationToken);
            if (vote.VoterId != userId)
            {
                await _cache.RemoveManyAsync(ResponseCacheKeys.AfterAskBackListChange(vote.VoterId), cancellationToken);
            }

            var details = await LoadDetailsAsync(conversationId, userId, cancellationToken);
            return (details, created);
        }

        public Task<AskBackPage<AskBackConversationListItemResponse>> ListConversationsAsync(
            Guid userId,
            string? cursor,
            int? limit,
            CancellationToken cancellationToken = default)
        {
            // Only cache the default page size so limit variants do not thrash Redis.
            var pageSize = ClampPageSize(limit, AskBackOptions.ConversationPageSize);
            if (pageSize != AskBackOptions.ConversationPageSize)
            {
                return LoadConversationsAsync(userId, cursor, pageSize, cancellationToken);
            }

            return _cache.GetOrSetAsync(
                ResponseCacheKeys.AskBackList(userId, cursor),
                _cacheOptions.AskBackListSoft,
                _cacheOptions.AskBackListHard,
                ct => LoadConversationsAsync(userId, cursor, pageSize, ct),
                cancellationToken);
        }

        private async Task<AskBackPage<AskBackConversationListItemResponse>> LoadConversationsAsync(
            Guid userId,
            string? cursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var page = await _repository.ListAsync(
                userId,
                AskBackCursor.TryDecode(cursor),
                pageSize,
                cancellationToken);

            if (page.Items.Count == 0)
            {
                return page;
            }

            var conversations = await _repository.FindForParticipantsAsync(
                page.Items.Select(item => item.Id).ToArray(),
                userId,
                cancellationToken);
            var byId = conversations.ToDictionary(conversation => conversation.Id);
            var purchaseLimit = await _entitlements.GetPurchaseMessageLimitAsync(cancellationToken);
            var accessByConversation = new Dictionary<Guid, (int? MessageLimit, bool Paused)>();
            var aligned = false;

            foreach (var group in conversations.GroupBy(c => c.CreatorUserId))
            {
                if (group.Key is not { } askerId)
                {
                    foreach (var conversation in group)
                    {
                        accessByConversation[conversation.Id] = (purchaseLimit, false);
                    }

                    continue;
                }

                var cards = group
                    .Select(c => (c.ContentType, c.ContentId))
                    .Distinct()
                    .ToArray();
                var batch = await _entitlements.GetCardAccessBatchAsync(askerId, cards, cancellationToken);
                foreach (var conversation in group)
                {
                    var access = batch.GetValueOrDefault((conversation.ContentType, conversation.ContentId));
                    var messageLimit = access?.MessageLimit ?? purchaseLimit;
                    var paused = access?.Paused ?? false;
                    if (conversation.TryAlignAskerQuota(messageLimit))
                    {
                        aligned = true;
                    }

                    accessByConversation[conversation.Id] = (messageLimit, paused);
                }
            }

            if (aligned)
            {
                await _repository.SaveAsync(cancellationToken);
            }

            foreach (var item in page.Items)
            {
                if (!byId.TryGetValue(item.Id, out var conversation))
                {
                    continue;
                }

                var (messageLimit, paused) = accessByConversation.GetValueOrDefault(
                    item.Id,
                    (purchaseLimit, false));
                var turn = conversation.TurnFor(userId, messageLimit);
                item.Turn = turn.ToString().ToLowerInvariant();
                item.MessagesLeft = conversation.MessagesLeft(userId, messageLimit);
                item.IsPaused = paused;
                item.CanSend = conversation.CanSend && !paused && turn == AskBackTurn.Me;
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
            var access = await ResolveAccessAndAlignAsync(conversation, cancellationToken);
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

            var delivery = recipientId is { } deliveryRecipient
                ? AskBackMessageDeliveryTracking.Create(message.Id, deliveryRecipient, now)
                : null;

            var (messageId, created) = await _repository.AddMessageAsync(
                conversation, message, notification, outbox, delivery, cancellationToken);
            if (!created)
            {
                return await _repository.FindByClientMessageIdAsync(
                        conversationId, userId, request.ClientMessageId, cancellationToken)
                    ?? throw NotFound("Message not found.");
            }

            if (delivery is not null)
            {
                _logger.LogInformation(
                    "Ask Back delivery tracking created for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} with status {DeliveryStatus} at {Timestamp}.",
                    message.Id, conversationId, delivery.RecipientUserId, delivery.Status, now);
            }

            var senderName = await _repository.GetAnonymousNameAsync(userId, cancellationToken)
                ?? AskBackOptions.DeletedUserName;
            var forSender = ToResponse(message, senderName, isMine: true);
            var forRecipient = ToResponse(message, senderName, isMine: false);

            // The message is saved; delivery is best effort. A client that misses this reads it from the history.
            // The recipient goes first so a failure of the untracked echo cannot be mistaken for theirs.
            if (recipientId is { } target)
            {
                await DeliverToRecipientAsync(conversationId, message.Id, target, forRecipient, cancellationToken);
            }

            // The sender's other devices get the copy with isMine = true.
            await TryPublishAsync(
                () => _realtime.SendMessageToUserAsync(userId, forSender, cancellationToken),
                "message");

            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterAskBackListChange(userId), cancellationToken);
            if (recipientId is { } other)
            {
                await _cache.RemoveManyAsync(ResponseCacheKeys.AfterAskBackListChange(other), cancellationToken);
                await _cache.RemoveManyAsync(ResponseCacheKeys.AfterNotificationChange(other), cancellationToken);
            }

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

        public const int BlocksDefaultPageSize = 20;
        public const int BlocksMaxPageSize = 50;

        public async Task<(IReadOnlyList<AskBackBlockItemResponse> Items, PaginationMeta Meta)> ListBlocksAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? BlocksDefaultPageSize : pageSize, 1, BlocksMaxPageSize);

            var (blocks, total) = await _repository.ListBlocksForBlockerAsync(
                userId, page, pageSize, cancellationToken);

            var items = new List<AskBackBlockItemResponse>(blocks.Count);
            foreach (var block in blocks)
            {
                var name = await _repository.GetAnonymousNameAsync(block.BlockedUserId, cancellationToken)
                    ?? AskBackOptions.DeletedUserName;
                items.Add(new AskBackBlockItemResponse
                {
                    BlockId = block.Id,
                    AnonymousName = name,
                    BlockedAt = block.CreatedAt,
                    SourceConversationId = block.SourceConversationId
                });
            }

            return (items, PaginationMeta.Create(page, pageSize, total));
        }

        public async Task UnblockByBlockIdAsync(
            Guid userId,
            Guid blockId,
            CancellationToken cancellationToken = default)
        {
            var block = await _repository.FindOwnedBlockAsync(userId, blockId, cancellationToken)
                ?? throw NotFound("Block not found.");

            var blockedUserId = block.BlockedUserId;
            var conversations = await _repository.ListBlockedConversationsByPairAsync(
                userId, blockedUserId, cancellationToken);

            foreach (var conversation in conversations)
            {
                conversation.TryUnblock(userId);
            }

            await _repository.RemoveUserBlockAsync(userId, blockedUserId, cancellationToken);
            await _repository.SaveAsync(cancellationToken);

            foreach (var conversation in conversations)
            {
                await PublishStatusAsync(conversation, cancellationToken);
            }
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

        public async Task<AskBackAckOutcome> AcknowledgeDeliveryAsync(
            Guid userId,
            Guid messageId,
            CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            if (userId == Guid.Empty || messageId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Ask Back invalid acknowledgment rejected for message {MessageId} from user {UserId} at {Timestamp}.",
                    messageId, userId, now);
                return AskBackAckOutcome.NotFound;
            }

            _logger.LogInformation(
                "Ask Back acknowledgment received for message {MessageId} from recipient {RecipientUserId} at {Timestamp}.",
                messageId, userId, now);

            AskBackAckResult result;
            try
            {
                result = await _repository.AcknowledgeDeliveryAsync(messageId, userId, now, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Nothing changed; the client keeps the acknowledgment and sends it again.
                _logger.LogError(
                    exception,
                    "Ask Back acknowledgment could not be saved for message {MessageId} from recipient {RecipientUserId} at {Timestamp}.",
                    messageId, userId, now);
                throw;
            }

            switch (result.Outcome)
            {
                case AskBackAckOutcome.Delivered:
                    _logger.LogInformation(
                        "Ask Back message {MessageId} in conversation {ConversationId} marked {DeliveryStatus} for recipient {RecipientUserId} at {Timestamp} after {LatencyMs} ms.",
                        messageId,
                        result.ConversationId,
                        AskBackDeliveryStatus.Delivered,
                        userId,
                        result.DeliveredAt,
                        result.SentAt is { } sentAt && result.DeliveredAt is { } deliveredAt
                            ? (deliveredAt - sentAt).TotalMilliseconds
                            : null);
                    break;
                case AskBackAckOutcome.AlreadyDelivered:
                    _logger.LogInformation(
                        "Ask Back duplicate acknowledgment ignored for message {MessageId} in conversation {ConversationId} from recipient {RecipientUserId}; status stays {DeliveryStatus} since {DeliveredAt}.",
                        messageId, result.ConversationId, userId, AskBackDeliveryStatus.Delivered, result.DeliveredAt);
                    break;
                default:
                    _logger.LogWarning(
                        "Ask Back invalid acknowledgment rejected for message {MessageId} from user {UserId} at {Timestamp}.",
                        messageId, userId, now);
                    break;
            }

            return result.Outcome;
        }

        /// <summary>
        /// Records the attempt, then sends to the recipient's connections. Tracking writes never stop the send: a
        /// failed write is logged and the message still goes out; the sweeper settles rows left behind.
        /// </summary>
        private async Task DeliverToRecipientAsync(
            Guid conversationId,
            Guid messageId,
            Guid recipientId,
            AskBackMessageResponse payload,
            CancellationToken cancellationToken)
        {
            // Without a connection that acknowledges, a send cannot be confirmed, so it is not counted as an attempt.
            var status = _presence.HasAckCapableConnection(recipientId)
                ? AskBackDeliveryStatus.Sent
                : AskBackDeliveryStatus.NoAckConnection;
            var attemptAt = _time.GetUtcNow().UtcDateTime;
            try
            {
                await _repository.MarkDeliveryAttemptAsync(messageId, recipientId, status, attemptAt, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Ask Back delivery tracking could not be set to {DeliveryStatus} for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} at {Timestamp}.",
                    status, messageId, conversationId, recipientId, attemptAt);
            }

            _logger.LogInformation(
                "Ask Back delivery attempt started for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} with status {DeliveryStatus} at {Timestamp}.",
                messageId, conversationId, recipientId, status, attemptAt);
            try
            {
                await _realtime.SendMessageToUserAsync(recipientId, payload, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                var failedAt = _time.GetUtcNow().UtcDateTime;
                _logger.LogWarning(
                    exception,
                    "Ask Back delivery attempt failed for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} with status {DeliveryStatus} at {Timestamp}.",
                    messageId, conversationId, recipientId, AskBackDeliveryStatus.Failed, failedAt);
                try
                {
                    await _repository.MarkDeliveryFailedAsync(messageId, recipientId, failedAt, cancellationToken);
                }
                catch (Exception trackingException) when (trackingException is not OperationCanceledException)
                {
                    _logger.LogError(
                        trackingException,
                        "Ask Back delivery tracking could not be set to {DeliveryStatus} for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} at {Timestamp}.",
                        AskBackDeliveryStatus.Failed, messageId, conversationId, recipientId, failedAt);
                }

                return;
            }

            _logger.LogInformation(
                "Ask Back delivery attempt completed for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId} with status {DeliveryStatus} at {Timestamp}.",
                messageId, conversationId, recipientId, status, _time.GetUtcNow().UtcDateTime);
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

        /// <summary>
        /// Resolves the live asker limit and, when that limit has risen, grandfathers prior asker sends so the new
        /// budget is full. The conversation must be tracked so the alignment can be saved.
        /// </summary>
        private async Task<(int? MessageLimit, bool Paused)> ResolveAccessAndAlignAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken)
        {
            var access = await ResolveAccessAsync(conversation, cancellationToken);
            if (conversation.TryAlignAskerQuota(access.MessageLimit))
            {
                await _repository.SaveAsync(cancellationToken);
            }

            return access;
        }

        /// <summary>The conversation as the caller sees it, with whose turn it is and how many messages are left.</summary>
        private async Task<AskBackConversationResponse> LoadDetailsAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            var response = await _repository.GetDetailsAsync(conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");
            // Tracked: raising the plan limit must persist AskerQuotaOffset before turn/left are computed.
            var conversation = await _repository.FindForParticipantAsync(
                conversationId, userId, cancellationToken)
                ?? throw NotFound("Conversation not found.");

            var (limit, paused) = await ResolveAccessAndAlignAsync(conversation, cancellationToken);
            var turn = conversation.TurnFor(userId, limit);
            response.Turn = turn.ToString().ToLowerInvariant();
            // Asker-only budget; voters reply on turn and get null.
            response.MessagesLeft = conversation.MessagesLeft(userId, limit);
            response.IsPaused = paused;
            response.CanSend = response.CanSend && !paused && turn == AskBackTurn.Me;
            if (turn == AskBackTurn.Done && response.MyRole == "asker")
            {
                response.WaitingReplies = await _repository.CountReasonsOnCardAsync(
                    conversation.ContentType, conversation.ContentId, cancellationToken);
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
