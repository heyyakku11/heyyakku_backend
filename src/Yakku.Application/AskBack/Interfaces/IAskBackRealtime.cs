using Yakku.Application.AskBack.DTOs;

namespace Yakku.Application.AskBack.Interfaces
{
    /// <summary>
    /// Pushes events to connected clients. It only delivers: the database is the source of truth, and a client that
    /// was offline reads what it missed from the history endpoint. Payloads carry anonymous names only.
    /// </summary>
    public interface IAskBackRealtime
    {
        /// <summary>The same message for both sides; <c>isMine</c> differs.</summary>
        Task MessageCreatedAsync(
            Guid conversationId,
            Guid senderUserId,
            Guid? recipientUserId,
            AskBackMessageResponse forSender,
            AskBackMessageResponse forRecipient,
            CancellationToken cancellationToken = default);

        Task MessageReadAsync(
            Guid conversationId,
            Guid? otherUserId,
            Guid lastReadMessageId,
            DateTime readAt,
            CancellationToken cancellationToken = default);

        /// <summary>Closed, blocked or reopened (status active).</summary>
        Task StatusChangedAsync(
            Guid conversationId,
            IReadOnlyCollection<Guid> userIds,
            string status,
            CancellationToken cancellationToken = default);
    }

    /// <summary>Whether a user currently has a live connection joined to a conversation.</summary>
    public interface IAskBackPresence
    {
        bool IsViewing(Guid conversationId, Guid userId);
    }

    /// <summary>Used where no hub is hosted (tests, workers without the API).</summary>
    public sealed class NullAskBackRealtime : IAskBackRealtime
    {
        public Task MessageCreatedAsync(Guid conversationId, Guid senderUserId, Guid? recipientUserId, AskBackMessageResponse forSender, AskBackMessageResponse forRecipient, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MessageReadAsync(Guid conversationId, Guid? otherUserId, Guid lastReadMessageId, DateTime readAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StatusChangedAsync(Guid conversationId, IReadOnlyCollection<Guid> userIds, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class NullAskBackPresence : IAskBackPresence
    {
        public bool IsViewing(Guid conversationId, Guid userId) => false;
    }
}
