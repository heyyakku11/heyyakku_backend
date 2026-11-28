using Yakku.Application.AskBack.DTOs;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AskBack.Interfaces
{
    /// <summary>A vote as seen by Ask Back: who owns the content, who voted, and whether the vote has a reason.</summary>
    public sealed record AskBackVoteContext(
        ShareContentType ContentType,
        Guid ContentId,
        Guid VoteId,
        Guid OwnerId,
        Guid VoterId,
        VoterRole VoterRole,
        bool HasReason,
        int OtherVotes);

    /// <summary>Result of a recipient acknowledging a message.</summary>
    public enum AskBackAckOutcome
    {
        /// <summary>This acknowledgment moved the delivery to Delivered.</summary>
        Delivered = 0,

        /// <summary>It was already Delivered (a duplicate or concurrent acknowledgment); nothing changed.</summary>
        AlreadyDelivered = 1,

        /// <summary>No delivery of that message to this user: unknown message, or not its recipient.</summary>
        NotFound = 2
    }

    /// <summary>The outcome, and the stored row's times for logging (null when not found).</summary>
    public sealed record AskBackAckResult(
        AskBackAckOutcome Outcome,
        Guid? ConversationId,
        DateTime? SentAt,
        DateTime? DeliveredAt);

    public interface IAskBackRepository
    {
        Task<AskBackVoteContext?> FindVoteContextAsync(Guid voteId, CancellationToken cancellationToken = default);

        /// <summary>The given users that exist, are enabled, and are not pending deletion.</summary>
        Task<HashSet<Guid>> GetUsableUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the user lets the people they answered ask them back (their "allow ask back" setting).
        /// </summary>
        Task<bool> AcceptsAskBacksAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<string?> GetAnonymousNameAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<Guid?> FindConversationIdByVoteAsync(Guid voteId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts the conversation with its participants in one transaction. If another request created the
        /// conversation for the same vote first (the unique index), nothing is inserted and that one's id is returned.
        /// </summary>
        Task<(Guid ConversationId, bool Created)> AddOrGetAsync(
            AskBackConversation conversation,
            CancellationToken cancellationToken = default);

        /// <summary>The conversation with its participants, tracked; null unless <paramref name="userId"/> is a participant.</summary>
        Task<AskBackConversation?> FindForParticipantAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>Tracked conversations the user participates in, for a page of ids (one query).</summary>
        Task<IReadOnlyList<AskBackConversation>> FindForParticipantsAsync(
            IReadOnlyCollection<Guid> conversationIds,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<bool> IsParticipantAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// A fresh, untracked copy of the conversation for working out turns and limits; null unless
        /// <paramref name="userId"/> is a participant.
        /// </summary>
        Task<AskBackConversation?> GetSnapshotAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<AskBackConversationResponse?> GetDetailsAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<AskBackPage<AskBackConversationListItemResponse>> ListAsync(
            Guid userId,
            (DateTime Timestamp, Guid Id)? cursor,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// One page of messages. Older: the newest messages before the cursor (the newest page without one);
        /// newer: the oldest messages after the cursor. Items are always oldest first. Null when not a participant.
        /// </summary>
        Task<AskBackPage<AskBackMessageResponse>?> ListMessagesAsync(
            Guid conversationId,
            Guid userId,
            (DateTime Timestamp, Guid Id)? cursor,
            bool newer,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<AskBackMessageResponse?> FindByClientMessageIdAsync(
            Guid conversationId,
            Guid senderUserId,
            Guid clientMessageId,
            CancellationToken cancellationToken = default);

        Task<AskBackMessage?> FindMessageAsync(
            Guid conversationId,
            Guid messageId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the message, the conversation's last-message time, the push row and the delivery tracking row in
        /// one transaction. A retry that races with the first send (the idempotency index) returns the stored
        /// message with <c>Created = false</c> and adds nothing.
        /// </summary>
        Task<(Guid MessageId, bool Created)> AddMessageAsync(
            AskBackConversation conversation,
            AskBackMessage message,
            Notification? notification,
            AskBackOutboxEvent? outbox,
            AskBackMessageDeliveryTracking? delivery,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the SignalR send attempt (<see cref="AskBackDeliveryStatus.Sent"/> or
        /// <see cref="AskBackDeliveryStatus.NoAckConnection"/>) with its time. Only a Pending row moves; false otherwise.
        /// </summary>
        Task<bool> MarkDeliveryAttemptAsync(
            Guid messageId,
            Guid recipientUserId,
            AskBackDeliveryStatus status,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>The send threw. Only a Pending or Sent row moves to Failed; false otherwise.</summary>
        Task<bool> MarkDeliveryFailedAsync(
            Guid messageId,
            Guid recipientUserId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves the delivery of <paramref name="messageId"/> to <paramref name="userId"/> to Delivered, in one
        /// conditional update: only the recipient's own row matches, and a row already Delivered is left alone, so
        /// duplicate and concurrent acknowledgments change it once.
        /// </summary>
        Task<AskBackAckResult> AcknowledgeDeliveryAsync(
            Guid messageId,
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task<int> CountUnreadAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// How many reasons are still on the card (option reasons and custom answers with a reason). Used for the
        /// unlock CTA when the free ask back is used up.
        /// </summary>
        Task<int> CountReasonsOnCardAsync(
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>The conversation top-up options that are on sale now (price, questions added), cheapest first.</summary>
        Task<IReadOnlyList<AskBackTopUpOptionResponse>> ListTopUpOptionsAsync(
            CancellationToken cancellationToken = default);

        /// <summary>True when either user blocked the other.</summary>
        Task<bool> IsBlockedEitherWayAsync(Guid userA, Guid userB, CancellationToken cancellationToken = default);

        /// <summary>Stages a user block (saved with the next <see cref="SaveAsync"/>); does nothing if there is one.</summary>
        Task AddUserBlockAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            Guid sourceConversationId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>Stages the removal of a user block (saved with the next <see cref="SaveAsync"/>).</summary>
        Task RemoveUserBlockAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default);

        /// <summary>Blocks the caller placed, newest first.</summary>
        Task<(IReadOnlyList<UserBlock> Items, int Total)> ListBlocksForBlockerAsync(
            Guid blockerUserId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        /// <summary>A block the caller owns, or null.</summary>
        Task<UserBlock?> FindOwnedBlockAsync(
            Guid blockerUserId,
            Guid blockId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Conversations between the pair that are blocked by <paramref name="blockerUserId"/> (tracked for unblock).
        /// </summary>
        Task<IReadOnlyList<AskBackConversation>> ListBlockedConversationsByPairAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default);

        void AddReport(AskBackReport report);

        /// <summary>Saves changes made to tracked entities (status, read position, report).</summary>
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
