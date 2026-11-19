using Yakku.Domain.Enums;

namespace Yakku.Application.Entitlements
{
    /// <summary>A plan's perks, as the entitlement rules read them.</summary>
    public sealed record PlanPerks(
        Guid Id,
        string Code,
        string Name,
        int? DurationDays,
        int? UnlockTickets,
        int? AskBackMessageLimit);

    /// <summary>The plan row a user is on, with the dates of the current activation.</summary>
    /// <param name="EntitlementId">
    /// Active <c>user_plan_entitlements</c> row when present; Lite tickets are scoped to this id.
    /// </param>
    public sealed record UserPlanState(
        PlanPerks Plan,
        DateTime? ActivatedAt,
        DateTime? ExpiresAt,
        Guid? EntitlementId = null);

    /// <summary>
    /// The plan that counts for a user right now. A paid plan whose end has passed is replaced by the free plan
    /// (<see cref="Expired"/> is then true so the app can say so).
    /// </summary>
    public sealed record EffectivePlan(
        PlanPerks Plan,
        bool IsPaid,
        bool Expired,
        DateTime? ActivatedAt,
        DateTime? ExpiresAt,
        Guid? EntitlementId = null);

    /// <summary>What an owner can do with Ask Back on one card.</summary>
    /// <param name="Unlocked">The card has an unlock.</param>
    /// <param name="Source">How it was unlocked; null when it is not.</param>
    /// <param name="Paused">Unlocked through a plan that has expired: read-only for both people.</param>
    /// <param name="MessageLimit">
    /// Questions the asker may send per reason in a conversation on this card now; null means no limit. A card with no
    /// unlock gets the free limit (only a free top-reason ask back can exist there).
    /// </param>
    /// <param name="PurchaseMessageLimit">What a card unlocked on its own gets: the free plan's limit.</param>
    /// <param name="TicketsLeft">Tickets left on the active plan; null means unlimited (or no active plan).</param>
    /// <param name="CanUnlockWithPlan">An active plan can unlock this card now (a ticket is left, or it is unlimited).</param>
    /// <param name="CanAutoUnlock">The active plan has no ticket limit, so asking back unlocks the card by itself.</param>
    public sealed record CardAccess(
        bool Unlocked,
        CardUnlockSource? Source,
        bool Paused,
        int? MessageLimit,
        int? PurchaseMessageLimit,
        int? TicketsLeft,
        bool CanUnlockWithPlan,
        bool CanAutoUnlock,
        EffectivePlan Plan)
    {
        /// <summary>
        /// The owner can read every reason on the card: it was bought on its own, or an active paid plan covers the
        /// owner. Otherwise only the free top reason of each option is readable and the rest are hidden.
        /// </summary>
        public bool ReasonsRevealed => Source == CardUnlockSource.Purchase || Plan.IsPaid;
    }
}
