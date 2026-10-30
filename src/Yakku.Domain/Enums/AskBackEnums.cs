namespace Yakku.Domain.Enums
{
    public enum AskBackStatus
    {
        /// <summary>Both participants can send messages.</summary>
        Active = 0,

        /// <summary>Ended by a participant. Read-only; only the asker (poll creator) can reopen it.</summary>
        Closed = 1,

        /// <summary>One participant blocked the other. Read-only; only the blocker can lift it.</summary>
        Blocked = 2
    }

    /// <summary>Delivery state of the push for one Ask Back message.</summary>
    public enum AskBackOutboxStatus
    {
        Pending = 0,
        Processing = 1,
        Sent = 2,
        Skipped = 3,
        Failed = 4
    }
}
