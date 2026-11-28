namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One paid conversation top-up: the questions it added to an ask back conversation. One row per payment order, so
    /// a retried fulfillment cannot grant the same purchase twice.
    /// </summary>
    public class AskBackConversationTopUp
    {
        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }

        /// <summary>The paid order this grant came from. Unique: the idempotency key of the grant.</summary>
        public Guid PaymentOrderId { get; private set; }

        /// <summary>Questions added to the conversation.</summary>
        public int Credits { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private AskBackConversationTopUp()
        {
        }

        public static AskBackConversationTopUp Create(Guid conversationId, Guid paymentOrderId, int credits, DateTime utcNow)
        {
            if (conversationId == Guid.Empty)
            {
                throw new ArgumentException("Conversation id is required.", nameof(conversationId));
            }

            if (paymentOrderId == Guid.Empty)
            {
                throw new ArgumentException("Payment order id is required.", nameof(paymentOrderId));
            }

            if (credits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(credits));
            }

            return new AskBackConversationTopUp
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                PaymentOrderId = paymentOrderId,
                Credits = credits,
                CreatedAt = utcNow
            };
        }
    }
}
