using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Yakku.API.Auth;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Auth.Interfaces;

namespace Yakku.API.AskBack
{
    /// <summary>
    /// Real-time Ask Back events at /hubs/ask-back. Messages are sent through the REST API, never here; the hub only
    /// delivers (ReceiveMessage, MessageRead, ConversationClosed, ConversationReopened, UserTyping,
    /// UserStoppedTyping) and takes delivery acknowledgments. A connection can join a conversation's group only if it
    /// is a participant, and group names are built on the server, so guessing a conversation id gets nothing.
    /// </summary>
    [Authorize]
    public sealed class AskBackHub : Hub
    {
        /// <summary>Query flag on the hub URL from app versions that call <see cref="AcknowledgeMessage"/>.</summary>
        public const string DeliveryAckQueryKey = "deliveryAck";

        private readonly IAskBackService _askBack;
        private readonly IUserSessionRepository _sessions;
        private readonly AskBackPresence _presence;

        public AskBackHub(IAskBackService askBack, IUserSessionRepository sessions, AskBackPresence presence)
        {
            _askBack = askBack;
            _sessions = sessions;
            _presence = presence;
        }

        public static string GroupName(Guid conversationId) => $"conversation:{conversationId:D}";

        public override async Task OnConnectedAsync()
        {
            // The token was valid at connect time; a logged-out session must not get a connection.
            var userId = await RequireLiveSessionAsync();
            var supportsAck = Context.GetHttpContext()?.Request.Query[DeliveryAckQueryKey].ToString() == "1";
            _presence.Connect(Context.ConnectionId, userId, supportsAck);
            await base.OnConnectedAsync();
        }

        /// <summary>
        /// The client received <paramref name="messageId"/>. Who acknowledges comes from the token only; anyone but
        /// the recipient, and an unknown id, get the same error. Acknowledging again is harmless.
        /// </summary>
        public async Task AcknowledgeMessage(Guid messageId)
        {
            var userId = await RequireLiveSessionAsync();
            AskBackAckOutcome outcome;
            try
            {
                outcome = await _askBack.AcknowledgeDeliveryAsync(userId, messageId, Context.ConnectionAborted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The service logged it. The client keeps the acknowledgment queued and sends it again.
                throw new HubException("Acknowledgment failed.");
            }

            if (outcome == AskBackAckOutcome.NotFound)
            {
                throw new HubException("Message not found.");
            }
        }

        public async Task JoinConversation(Guid conversationId)
        {
            var userId = await RequireLiveSessionAsync();
            if (!await _askBack.CanJoinAsync(userId, conversationId, Context.ConnectionAborted))
            {
                // The same answer for "does not exist" and "not yours".
                throw new HubException("Conversation not found.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId), Context.ConnectionAborted);
            _presence.Join(Context.ConnectionId, userId, conversationId);
        }

        public async Task LeaveConversation(Guid conversationId)
        {
            _presence.Leave(Context.ConnectionId, conversationId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(conversationId), Context.ConnectionAborted);
        }

        public Task TypingStarted(Guid conversationId)
        {
            return RelayTypingAsync(conversationId, "UserTyping");
        }

        public Task TypingStopped(Guid conversationId)
        {
            return RelayTypingAsync(conversationId, "UserStoppedTyping");
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            _presence.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        private async Task RelayTypingAsync(Guid conversationId, string eventName)
        {
            await RequireLiveSessionAsync();

            // Only a connection that joined (and so passed the participant check) can type into the group.
            if (!_presence.HasJoined(Context.ConnectionId, conversationId))
            {
                throw new HubException("Join the conversation first.");
            }

            await Clients.OthersInGroup(GroupName(conversationId))
                .SendAsync(eventName, new { conversationId }, Context.ConnectionAborted);
        }

        private async Task<Guid> RequireLiveSessionAsync()
        {
            var user = Context.User;
            if (user is null || !user.TryGetUserId(out var userId) || !user.TryGetSessionId(out var sessionId))
            {
                Context.Abort();
                throw new HubException("Unauthorized.");
            }

            if (!await _sessions.IsLiveAsync(sessionId, userId, DateTime.UtcNow, Context.ConnectionAborted))
            {
                Context.Abort();
                throw new HubException("Unauthorized.");
            }

            return userId;
        }
    }
}
