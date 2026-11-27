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

    /// <summary>Whose turn it is in a conversation, from one participant's point of view.</summary>
    public enum AskBackTurn
    {
        /// <summary>The caller can send now.</summary>
        Me = 0,

        /// <summary>The caller is waiting for the other person.</summary>
        Them = 1,

        /// <summary>Every question allowed was asked and answered; nobody can send again.</summary>
        Done = 2
    }

    /// <summary>Outcome of trying to record a message against the turn rule.</summary>
    public enum AskBackSendResult
    {
        Ok = 0,
        NotYourTurn = 1,
        LimitReached = 2
    }

    /// <summary>How a card was unlocked for Ask Back.</summary>
    public enum CardUnlockSource
    {
        /// <summary>By the owner's plan (a ticket, or automatically on an unlimited plan). Paused when the plan expires.</summary>
        Plan = 0,

        /// <summary>Bought on its own, or backfilled for a conversation that already existed. Never expires.</summary>
        Purchase = 1
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

    /// <summary>Real-time (SignalR) delivery state of one Ask Back message to its recipient.</summary>
    public enum AskBackDeliveryStatus
    {
        /// <summary>Stored with the message; no send attempted yet.</summary>
        Pending = 0,

        /// <summary>The SignalR send was attempted. Not proof of receipt: only an acknowledgment is.</summary>
        Sent = 1,

        /// <summary>The recipient's client acknowledged the message.</summary>
        Delivered = 2,

        /// <summary>The send threw, or it was never attempted (the row stayed Pending past the timeout).</summary>
        Failed = 3,

        /// <summary>
        /// Sent, but no acknowledgment within the timeout. The message may still have arrived (the acknowledgment
        /// can be lost), so a late acknowledgment still moves it to Delivered.
        /// </summary>
        TimedOut = 4,

        /// <summary>
        /// The recipient had no connection able to acknowledge (offline, or only app versions without
        /// acknowledgments). The send still happens; these rows are not counted as attempts.
        /// </summary>
        NoAckConnection = 5
    }
}
