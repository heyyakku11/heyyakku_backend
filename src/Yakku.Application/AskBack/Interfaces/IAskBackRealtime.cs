using Yakku.Application.AskBack.DTOs;

namespace Yakku.Application.AskBack.Interfaces
{
    /// <summary>
    /// Pushes events to connected clients. It only delivers: the database is the source of truth, and a client that
    /// was offline reads what it missed from the history endpoint. Payloads carry anonymous names only.
    /// </summary>
    public interface IAskBackRealtime
    {
        /// <summary>
        /// <c>ReceiveMessage</c> to every connection of one user. Completing means the send was handed to SignalR,
        /// not that a client received it; only the client's acknowledgment says that.
        /// </summary>
        Task SendMessageToUserAsync(
            Guid userId,
            AskBackMessageResponse message,
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

    /// <summary>Which users have live connections, and which conversations those connections joined.</summary>
    public interface IAskBackPresence
    {
        bool IsViewing(Guid conversationId, Guid userId);

        /// <summary>Whether the user has a live connection from an app that acknowledges received messages.</summary>
        bool HasAckCapableConnection(Guid userId);
    }

    /// <summary>Used where no hub is hosted (tests, workers without the API).</summary>
    public sealed class NullAskBackRealtime : IAskBackRealtime
    {
        public Task SendMessageToUserAsync(Guid userId, AskBackMessageResponse message, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MessageReadAsync(Guid conversationId, Guid? otherUserId, Guid lastReadMessageId, DateTime readAt, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StatusChangedAsync(Guid conversationId, IReadOnlyCollection<Guid> userIds, string status, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public sealed class NullAskBackPresence : IAskBackPresence
    {
        public bool IsViewing(Guid conversationId, Guid userId) => false;

        public bool HasAckCapableConnection(Guid userId) => false;
    }
}
