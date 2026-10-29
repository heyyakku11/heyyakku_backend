namespace Yakku.Domain.Enums
{
    /// <summary>How a vote's notification reaches the asker.</summary>
    public enum VoteNotificationKind
    {
        /// <summary>One of the card's first votes: pushed on its own.</summary>
        Immediate = 0,

        /// <summary>A later vote: only counted in its window's digest push.</summary>
        Digest = 1
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
