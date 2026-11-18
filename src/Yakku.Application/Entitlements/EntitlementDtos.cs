namespace Yakku.Application.Entitlements
{
    /// <summary>Unlock a card for Ask Back, named by one of its votes (the reason's id).</summary>
    public class UnlockCardRequest
    {
        public Guid VoteId { get; set; }
    }

    /// <summary>What the owner can do with Ask Back on a card, and what their plan allows.</summary>
    public class AskBackAccessResponse
    {
        /// <summary>The card is unlocked: every reason on it can be asked back.</summary>
        public bool Unlocked { get; set; }

        /// <summary>plan or purchase when unlocked; null otherwise.</summary>
        public string? UnlockSource { get; set; }

        /// <summary>Unlocked through a plan that has since expired: chats on the card are read-only.</summary>
        public bool Paused { get; set; }

        public string PlanCode { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;

        /// <summary>When the paid plan ends; null on the free plan.</summary>
        public DateTime? PlanExpiresAt { get; set; }

        /// <summary>A paid plan was bought and has ended.</summary>
        public bool PlanExpired { get; set; }

        /// <summary>Tickets left on the active plan; null when there is no limit, or no active plan.</summary>
        public int? TicketsLeft { get; set; }

        /// <summary>The active plan can unlock this card now: a ticket is left, or it has no ticket limit.</summary>
        public bool CanUnlock { get; set; }

        /// <summary>Questions the asker can send per reason on this card; see <see cref="UnlimitedMessages"/>.</summary>
        public int? MessageLimit { get; set; }

        /// <summary>True when there is no limit on questions per reason.</summary>
        public bool UnlimitedMessages { get; set; }
    }

    public class ActivatePlanRequest
    {
        public string? PlanCode { get; set; }
    }

    public class ActivatePlanResponse
    {
        public Guid UserId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class GrantCardUnlockRequest
    {
        /// <summary>poll or check.</summary>
        public string? ContentType { get; set; }
        public Guid ContentId { get; set; }
    }

    public class GrantCardUnlockResponse
    {
        public Guid UserId { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public Guid ContentId { get; set; }

        /// <summary>plan or purchase.</summary>
        public string Source { get; set; } = string.Empty;
    }
}
