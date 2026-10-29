using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A fixed window of a card's later votes. <see cref="EndsAt"/> is the eligibility cutoff: events read before
    /// it are left out of the digest.
    /// </summary>
    public class VoteDigestWindow
    {
        public Guid Id { get; private set; }
        public Guid OwnerId { get; private set; }
        public ShareContentType ContentType { get; private set; }
        public Guid ContentId { get; private set; }
        public DateTime StartsAt { get; private set; }
        public DateTime EndsAt { get; private set; }
        public VoteDigestWindowStatus Status { get; private set; }
        public int Attempts { get; private set; }
        public DateTime? NextAttemptAt { get; private set; }
        public DateTime? LeaseUntil { get; private set; }
        public Guid? LeaseToken { get; private set; }
        public int EventCount { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        private VoteDigestWindow()
        {
        }

        public static VoteDigestWindow Open(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            DateTime startsAt,
            TimeSpan length)
        {
            return new VoteDigestWindow
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ContentType = contentType,
                ContentId = contentId,
                StartsAt = startsAt,
                EndsAt = startsAt + length,
                Status = VoteDigestWindowStatus.Open
            };
        }

        /// <summary>Stops accepting events; the dispatcher sends it next.</summary>
        public void MarkDue()
        {
            Status = VoteDigestWindowStatus.Due;
        }
    }
}
