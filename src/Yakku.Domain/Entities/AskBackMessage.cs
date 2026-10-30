namespace Yakku.Domain.Entities
{
    public class AskBackMessage
    {
        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }

        /// <summary>Null once the sender's account was permanently deleted.</summary>
        public Guid? SenderUserId { get; private set; }
        public string Content { get; private set; } = string.Empty;

        /// <summary>Chosen by the sending client; makes a retried send idempotent.</summary>
        public Guid ClientMessageId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? EditedAt { get; private set; }
        public DateTime? DeletedAt { get; private set; }

        public AskBackConversation Conversation { get; private set; } = null!;

        private AskBackMessage()
        {
        }

        public static AskBackMessage Create(
            Guid conversationId,
            Guid senderUserId,
            Guid clientMessageId,
            string content,
            DateTime utcNow)
        {
            return new AskBackMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                SenderUserId = senderUserId,
                ClientMessageId = clientMessageId,
                Content = content,
                CreatedAt = utcNow
            };
        }
    }
}
