using Yakku.Application.AskBack.Interfaces;

namespace Yakku.API.AskBack
{
    /// <summary>
    /// Which connections have joined which conversation's group. A user can have several connections (devices,
    /// tabs); they are viewing a conversation while any of them is joined. Held in memory: it is only used to
    /// skip a push for someone already looking at the chat, so losing it on a restart costs at most one push.
    /// </summary>
    public sealed class AskBackPresence : IAskBackPresence
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, (Guid UserId, HashSet<Guid> Conversations)> _connections = new();

        public void Join(string connectionId, Guid userId, Guid conversationId)
        {
            lock (_gate)
            {
                if (!_connections.TryGetValue(connectionId, out var entry))
                {
                    entry = (userId, new HashSet<Guid>());
                    _connections[connectionId] = entry;
                }

                entry.Conversations.Add(conversationId);
            }
        }

        public void Leave(string connectionId, Guid conversationId)
        {
            lock (_gate)
            {
                if (_connections.TryGetValue(connectionId, out var entry))
                {
                    entry.Conversations.Remove(conversationId);
                }
            }
        }

        public void Remove(string connectionId)
        {
            lock (_gate)
            {
                _connections.Remove(connectionId);
            }
        }

        /// <summary>Whether this connection joined the conversation (typing events need it).</summary>
        public bool HasJoined(string connectionId, Guid conversationId)
        {
            lock (_gate)
            {
                return _connections.TryGetValue(connectionId, out var entry) && entry.Conversations.Contains(conversationId);
            }
        }

        public bool IsViewing(Guid conversationId, Guid userId)
        {
            lock (_gate)
            {
                return _connections.Values.Any(entry =>
                    entry.UserId == userId && entry.Conversations.Contains(conversationId));
            }
        }
    }
}
