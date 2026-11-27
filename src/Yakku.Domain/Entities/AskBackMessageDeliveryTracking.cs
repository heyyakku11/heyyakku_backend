using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// The real-time delivery of one Ask Back message to its recipient: written with the message (Pending), moved to
    /// Sent just before the SignalR send, and to Delivered only when the recipient's client acknowledges it. Status
    /// changes are conditional updates in the repository, never through this tracked entity, so they cannot race.
    /// </summary>
    public class AskBackMessageDeliveryTracking
    {
        public Guid Id { get; private set; }
        public Guid MessageId { get; private set; }

        /// <summary>Null once the recipient's account was permanently deleted; the row stays for the metrics.</summary>
        public Guid? RecipientUserId { get; private set; }
        public AskBackDeliveryStatus Status { get; private set; }

        /// <summary>When the server attempted the SignalR send.</summary>
        public DateTime? SentAt { get; private set; }

        /// <summary>When the recipient's client acknowledged the message.</summary>
        public DateTime? DeliveredAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public AskBackMessage Message { get; private set; } = null!;

        private AskBackMessageDeliveryTracking()
        {
        }

        public static AskBackMessageDeliveryTracking Create(Guid messageId, Guid recipientUserId, DateTime utcNow)
        {
            return new AskBackMessageDeliveryTracking
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                RecipientUserId = recipientUserId,
                Status = AskBackDeliveryStatus.Pending,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
        }
    }
}
