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

    public interface IAskBackRepository
    {
        Task<AskBackVoteContext?> FindVoteContextAsync(Guid voteId, CancellationToken cancellationToken = default);

        /// <summary>The given users that exist, are enabled, and are not pending deletion.</summary>
        Task<HashSet<Guid>> GetUsableUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default);

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
            AskBackOutboxEvent? outbox,
            CancellationToken cancellationToken = default);

        Task<int> CountUnreadAsync(
            Guid conversationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        void AddReport(AskBackReport report);

        /// <summary>Saves changes made to tracked entities (status, read position, mute, report).</summary>
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
