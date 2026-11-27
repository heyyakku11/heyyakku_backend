using Microsoft.AspNetCore.SignalR;
using Yakku.API.Auth;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;

namespace Yakku.API.AskBack
{
    /// <summary>
    /// Delivers Ask Back events over SignalR to a user's connections (every device they are signed in on), not only
    /// the ones that joined the conversation, so the conversation list updates too. Payloads carry anonymous names
    /// and conversation/message ids only.
    /// </summary>
    public sealed class AskBackRealtime : IAskBackRealtime
    {
        private readonly IHubContext<AskBackHub> _hub;

        public AskBackRealtime(IHubContext<AskBackHub> hub)
        {
            _hub = hub;
        }

        public Task SendMessageToUserAsync(
            Guid userId,
            AskBackMessageResponse message,
            CancellationToken cancellationToken = default)
        {
            // Persisted before this is called. With no live connection SignalR completes without sending anything.
            return _hub.Clients.User(userId.ToString())
                .SendAsync("ReceiveMessage", message, cancellationToken);
        }

        public async Task MessageReadAsync(
            Guid conversationId,
            Guid? otherUserId,
            Guid lastReadMessageId,
            DateTime readAt,
            CancellationToken cancellationToken = default)
        {
            if (otherUserId is { } other)
            {
                await _hub.Clients.User(other.ToString())
                    .SendAsync("MessageRead", new { conversationId, lastReadMessageId, readAt }, cancellationToken);
            }
        }

        public async Task StatusChangedAsync(
            Guid conversationId,
            IReadOnlyCollection<Guid> userIds,
            string status,
            CancellationToken cancellationToken = default)
        {
            var eventName = status == "active" ? "ConversationReopened" : "ConversationClosed";
            foreach (var userId in userIds)
            {
                await _hub.Clients.User(userId.ToString())
                    .SendAsync(eventName, new { conversationId, status }, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Identifies a connection's user the same way the REST API does (name identifier or sub), so
    /// <c>Clients.User(id)</c> reaches the right connections whichever claim the token carries.
    /// </summary>
    public sealed class YakkuUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
        {
            return connection.User.TryGetUserId(out var userId) ? userId.ToString() : null;
        }
    }
}
