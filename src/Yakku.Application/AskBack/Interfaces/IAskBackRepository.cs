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

    /// <summary>Result of trying to start a conversation with the card's one free ask back.</summary>
    public enum FreeAskOutcome
    {
        /// <summary>The conversation was created.</summary>
        Created = 0,

        /// <summary>The card already has a conversation (another reason used its free ask back).</summary>
        CardAlreadyUsed = 1,

        /// <summary>This vote already had a conversation, created by a request that got there first.</summary>
        AlreadyExists = 2
    }

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
        /// Saves the message, the conversation's last-message time and the push row in one transaction. A retry
        /// that races with the first send (the idempotency index) returns the stored message with
        /// <c>Created = false</c>.
        /// </summary>
        Task<(Guid MessageId, bool Created)> AddMessageAsync(
            AskBackConversation conversation,
            AskBackMessage message,
            Notification? notification,
            AskBackOutboxEvent? outbox,
            CancellationToken cancellationToken = default);

        Task<int> CountUnreadAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Whether the vote is the top reason of its option: the newest visible reason a signed-in user gave for that
        /// option, the same one the owner's reasons list shows first. A custom answer's reason is always its own top.
        /// </summary>
        Task<bool> IsTopAskableReasonAsync(AskBackVoteContext vote, CancellationToken cancellationToken = default);

        /// <summary>Whether the card has a conversation about any of its reasons.</summary>
        Task<bool> HasConversationOnCardAsync(
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// How many reasons are still on the card (option reasons and custom answers with a reason). Used for the
        /// unlock CTA when the free ask back is used up.
        /// </summary>
        Task<int> CountReasonsOnCardAsync(
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the conversation only if the card has none yet, so a card gets one free ask back in all. The check
        /// and the insert run in one transaction under a lock on the card, so two simultaneous taps on different
        /// reasons cannot both create one.
        /// </summary>
        Task<(FreeAskOutcome Outcome, Guid? ConversationId)> AddFreeAskConversationAsync(
            AskBackConversation conversation,
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
