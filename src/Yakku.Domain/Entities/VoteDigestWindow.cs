using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A notification batch: the votes of one card collected for a single aggregated push. Table
    /// <c>vote_digest_windows</c> predates the adaptive batching, so some names are kept:
    /// <see cref="StartsAt"/> is the batch's FirstPendingAt, <see cref="EndsAt"/> is its NextFlushAt (the due time,
    /// moved by each vote up to <see cref="MaxFlushAt"/>) and <see cref="CompletedAt"/> is when it was sent.
    /// <see cref="EndsAt"/> is also the cutoff for read events: an event read before it is left out of the push.
    /// <see cref="Id"/> is the stable BatchId and the push's delivery id. <see cref="Status"/>: Open accepts events,
    /// Due is a batch that stopped accepting events and waits (also a failed send waiting to retry), Processing is
    /// claimed by a worker, then Sent, Skipped or Failed.
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

        /// <summary>The time of the most recent vote in the batch.</summary>
        public DateTime? LastVoteAt { get; private set; }

        /// <summary>The latest the batch may wait: FirstPendingAt plus the maximum wait. Continuous voting cannot pass it.</summary>
        public DateTime? MaxFlushAt { get; private set; }

        /// <summary>Raised on every reschedule. A write carrying an older generation never overwrites a newer one.</summary>
        public int Generation { get; private set; }

        /// <summary>When a worker last claimed the batch to send it.</summary>
        public DateTime? ClaimedAt { get; private set; }

        /// <summary>FCM message id of the accepted push.</summary>
        public string? ProviderMessageId { get; private set; }

        private VoteDigestWindow()
        {
        }

        public static VoteDigestWindow Open(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            DateTime firstPendingAt,
            DateTime nextFlushAt,
            DateTime maxFlushAt)
        {
            return new VoteDigestWindow
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                ContentType = contentType,
                ContentId = contentId,
                StartsAt = firstPendingAt,
                EndsAt = nextFlushAt,
                LastVoteAt = firstPendingAt,
                MaxFlushAt = maxFlushAt,
                Generation = 1,
                Status = VoteDigestWindowStatus.Open
            };
        }
    }
}
