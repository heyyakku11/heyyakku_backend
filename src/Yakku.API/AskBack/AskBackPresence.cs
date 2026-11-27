using Yakku.Application.AskBack.Interfaces;

namespace Yakku.API.AskBack
{
    /// <summary>
    /// Live connections, whether each one acknowledges received messages, and which conversations' groups it joined.
    /// A user can have several connections (devices, tabs); they are viewing a conversation while any of them is
    /// joined. Held in memory: it only skips a push for someone already looking at the chat and decides whether a
    /// delivery can be acknowledged, so losing it on a restart costs at most one push or one untracked delivery.
    /// </summary>
    public sealed class AskBackPresence : IAskBackPresence
    {
        private readonly object _gate = new();
        private readonly Dictionary<string, Entry> _connections = new();

        /// <summary>A connection was opened; <paramref name="supportsAck"/> comes from the client's hub URL.</summary>
        public void Connect(string connectionId, Guid userId, bool supportsAck)
        {
            lock (_gate)
            {
                if (_connections.TryGetValue(connectionId, out var entry))
                {
                    entry.SupportsAck = supportsAck;
                    return;
                }

                _connections[connectionId] = new Entry(userId) { SupportsAck = supportsAck };
            }
        }

        public void Join(string connectionId, Guid userId, Guid conversationId)
        {
            lock (_gate)
            {
                if (!_connections.TryGetValue(connectionId, out var entry))
                {
                    entry = new Entry(userId);
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

        public bool HasAckCapableConnection(Guid userId)
        {
            lock (_gate)
            {
                return _connections.Values.Any(entry => entry.UserId == userId && entry.SupportsAck);
            }
        }

        private sealed class Entry
        {
            public Entry(Guid userId)
            {
                UserId = userId;
            }

            public Guid UserId { get; }
            public bool SupportsAck { get; set; }
            public HashSet<Guid> Conversations { get; } = new();
        }
    }
}
