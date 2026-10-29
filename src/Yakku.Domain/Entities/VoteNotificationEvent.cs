using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Outbox row written in the same transaction as the vote. It holds the push-delivery state; whether the asker
    /// has read it is the linked <see cref="Notification"/>'s <c>IsRead</c>. It never stores the voter or the answer.
    /// </summary>
    public class VoteNotificationEvent
    {
        public Guid Id { get; private set; }
        public Guid OwnerId { get; private set; }
        public ShareContentType ContentType { get; private set; }
        public Guid ContentId { get; private set; }
        public Guid VoteId { get; private set; }

        /// <summary>This vote's position among the card's notifying votes (1-based).</summary>
        public int Sequence { get; private set; }

        public VoteNotificationKind Kind { get; private set; }
        public Guid NotificationId { get; private set; }
        public Guid? WindowId { get; private set; }
        public VoteNotificationStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? LeaseUntil { get; private set; }
        public Guid? LeaseToken { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? SubmittedAt { get; private set; }

        private VoteNotificationEvent()
        {
        }

        public static VoteNotificationEvent Immediate(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            int sequence,
            Guid notificationId,
            DateTime now)
        {
            return new VoteNotificationEvent
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ContentType = contentType,
                ContentId = contentId,
                VoteId = voteId,
                Sequence = sequence,
                Kind = VoteNotificationKind.Immediate,
                NotificationId = notificationId,
                Status = VoteNotificationStatus.Pending,
                NextAttemptAt = now,
                CreatedAt = now
            };
        }

        public static VoteNotificationEvent ForDigest(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            int sequence,
            Guid notificationId,
            Guid windowId,
            DateTime now)
        {
            return new VoteNotificationEvent
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ContentType = contentType,
                ContentId = contentId,
                VoteId = voteId,
                Sequence = sequence,
                Kind = VoteNotificationKind.Digest,
                NotificationId = notificationId,
                WindowId = windowId,
                Status = VoteNotificationStatus.Pending,
                CreatedAt = now
            };
        }
    }
}
