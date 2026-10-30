using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// The push for one message, written in the same transaction as the message so it cannot be lost. A worker
    /// claims it with a lease and sends it unless the recipient is viewing the conversation. No foreign keys, like
    /// the vote notification outbox: stale rows end up skipped and purged.
    /// </summary>
    public class AskBackOutboxEvent
    {
        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }
        public Guid MessageId { get; private set; }
        public Guid RecipientUserId { get; private set; }
        public AskBackOutboxStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime NextAttemptAt { get; private set; }
        public DateTime? LeaseUntil { get; private set; }
        public Guid? LeaseToken { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        private AskBackOutboxEvent()
        {
        }

        public static AskBackOutboxEvent ForMessage(
            Guid conversationId,
            Guid messageId,
            Guid recipientUserId,
            DateTime utcNow,
            TimeSpan delay)
        {
            return new AskBackOutboxEvent
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                MessageId = messageId,
                RecipientUserId = recipientUserId,
                Status = AskBackOutboxStatus.Pending,
                NextAttemptAt = utcNow + delay,
                CreatedAt = utcNow
            };
        }
    }
}
