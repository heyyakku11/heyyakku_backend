using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Entitlements
{
    public interface IEntitlementRepository
    {
        /// <summary>The user's plan and activation dates; null when the user does not exist.</summary>
        Task<UserPlanState?> GetUserPlanAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<PlanPerks?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);

        Task<PlanPerks?> GetPlanByCodeAsync(string code, CancellationToken cancellationToken = default);

        /// <summary>The unlock of a card, tracked so it can be changed; null when the card is not unlocked.</summary>
        Task<CardUnlock?> FindUnlockAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>Unlocks the owner made through a plan since <paramref name="since"/> (the tickets used).</summary>
        Task<int> CountPlanUnlocksSinceAsync(Guid ownerId, DateTime since, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a plan unlock if the owner still has a ticket: unlocks made through a plan since
        /// <paramref name="ticketsSince"/> must be fewer than <paramref name="tickets"/> (null means no limit).
        /// The count and the insert are serialised per owner, so two taps cannot spend the same ticket. Returns the
        /// card's unlock (new or already there), or null when no ticket is left.
        /// </summary>
        Task<CardUnlock?> TryAddPlanUnlockAsync(
            CardUnlock unlock,
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

        /// <summary>Puts the user on the plan from <paramref name="utcNow"/>; false when the user does not exist.</summary>
        Task<UserPlanState?> ActivatePlanAsync(
            Guid userId,
            Guid planId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
