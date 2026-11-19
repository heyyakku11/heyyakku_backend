using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Entitlements
{
    public interface IEntitlementRepository
    {
        /// <summary>
        /// The user's effective plan projection. Prefers an active <c>user_plan_entitlements</c> row; lazily marks
        /// expired actives. Null when the user does not exist.
        /// </summary>
        Task<UserPlanState?> GetUserPlanAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task<PlanPerks?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);

        Task<PlanPerks?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>The unlock of a card, tracked so it can be changed; null when the card is not unlocked.</summary>
        Task<CardUnlock?> FindUnlockAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Plan unlocks that count against the current Lite (or similar) allowance. When
        /// <paramref name="entitlementId"/> is set, only unlocks for that entitlement count; otherwise unlocks since
        /// <paramref name="since"/> are counted (legacy / pre-entitlement activations).
        /// </summary>
        Task<int> CountPlanUnlocksAsync(
            Guid ownerId,
            Guid? entitlementId,
            DateTime since,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a plan unlock if the owner still has a ticket. The count and the insert are serialised per owner.
        /// Returns the card's unlock (new or already there), or null when no ticket is left.
        /// </summary>
        Task<CardUnlock?> TryAddPlanUnlockAsync(
            CardUnlock unlock,
            Guid? entitlementId,
            DateTime ticketsSince,
            int? tickets,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves the unlock. If another request unlocked the same card first (the unique index), nothing is added
        /// and that unlock is returned.
        /// </summary>
        Task<CardUnlock> AddUnlockAsync(CardUnlock unlock, CancellationToken cancellationToken = default);

        /// <summary>Whether the user owns the poll, or the made check.</summary>
        Task<bool> OwnsCardAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Puts the user on the plan from <paramref name="utcNow"/>, writing <c>user_plan_entitlements</c> history
        /// (superseding any prior active paid row). Admin overrides use a null payment order id.
        /// </summary>
        Task<UserPlanState?> ActivatePlanAsync(
            Guid userId,
            Guid planId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
