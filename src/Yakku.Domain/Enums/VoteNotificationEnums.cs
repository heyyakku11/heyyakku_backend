namespace Yakku.Domain.Enums
{
    /// <summary>How a vote's notification reaches the asker.</summary>
    public enum VoteNotificationKind
    {
        /// <summary>One of the card's first votes: pushed on its own.</summary>
        Immediate = 0,

        /// <summary>A later vote: only counted in its batch's aggregated push.</summary>
        Digest = 1,

        /// <summary>
        /// A vote past the card's immediate votes that has been committed but not yet classified. The dispatcher
        /// decides from the card's recent voting frequency whether it becomes <see cref="Immediate"/> or
        /// <see cref="Digest"/>; until then it is plain durable outbox state and nothing is lost if Redis is down.
        /// </summary>
        Undecided = 2
    }

    /// <summary>How a card's votes past the immediate ones are delivered, decided per card from its voting frequency.</summary>
    public enum VoteDeliveryMode
    {
        /// <summary>Each vote gets its own push.</summary>
        Individual = 0,

        /// <summary>Votes are collected and sent as one aggregated push.</summary>
        Batch = 1
    }

    /// <summary>Push-delivery state of a vote notification. Read state lives on the inbox notification.</summary>
    public enum VoteNotificationStatus
    {
        Pending = 0,
        Sending = 1,
        Submitted = 2,
        Skipped = 3,
        Failed = 4
    }

    public enum VoteDigestWindowStatus
    {
        /// <summary>Accepting digest events until its end.</summary>
        Open = 0,

        /// <summary>No longer accepting events and waiting to be sent (also a failed send waiting to retry).</summary>
        Due = 1,

        Processing = 2,
        Sent = 3,
        Skipped = 4,
        Failed = 5
    }
}
