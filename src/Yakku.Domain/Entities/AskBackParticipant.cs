namespace Yakku.Domain.Entities
{
    /// <summary>One side of a conversation, with its own read position and mute switch.</summary>
    public class AskBackParticipant
    {
        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }

        /// <summary>Null once the account was permanently deleted.</summary>
        public Guid? UserId { get; private set; }

        /// <summary>The last message this participant has read. Not a foreign key; checked in code.</summary>
        public Guid? LastReadMessageId { get; private set; }

        /// <summary>Creation time of that message, so the read position orders without a lookup.</summary>
        public DateTime? LastReadMessageCreatedAt { get; private set; }
        public DateTime? LastReadAt { get; private set; }
        public bool IsMuted { get; private set; }
        public DateTime JoinedAt { get; private set; }

        public AskBackConversation Conversation { get; private set; } = null!;

        private AskBackParticipant()
        {
        }

        internal static AskBackParticipant Create(Guid conversationId, Guid userId, DateTime utcNow)
        {
            return new AskBackParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                UserId = userId,
                JoinedAt = utcNow
            };
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
        }

        /// <summary>
        /// Moves the read position forward to a message. A position at or before the current one is ignored
        /// (returns false), so a late or replayed request cannot move it backward.
        /// </summary>
        public bool TryAdvanceRead(Guid messageId, DateTime messageCreatedAt, DateTime utcNow)
        {
            if (LastReadMessageId is not null && LastReadMessageCreatedAt is { } current)
            {
                // Same order as PostgreSQL's uuid comparison (big-endian bytes), which the message queries use.
                var isNewer = messageCreatedAt > current
                    || (messageCreatedAt == current
                        && messageId.ToByteArray(bigEndian: true).AsSpan()
                            .SequenceCompareTo(LastReadMessageId.Value.ToByteArray(bigEndian: true)) > 0);
                if (!isNewer)
                {
                    return false;
                }
            }

            LastReadMessageId = messageId;
            LastReadMessageCreatedAt = messageCreatedAt;
            LastReadAt = utcNow;
            return true;
        }
    }
}
