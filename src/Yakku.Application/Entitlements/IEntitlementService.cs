using Yakku.Domain.Enums;

namespace Yakku.Application.Entitlements
{
    /// <summary>
    /// Who may ask back on which card. A card is unlocked by a plan ticket (yakku lite), automatically by an
    /// unlimited plan (yakku love), or on its own (a purchase). Plans expire; cards unlocked through one are paused
    /// until the owner has an active plan again.
    /// </summary>
    public interface IEntitlementService
    {
        /// <summary>The owner's access on one card, read now.</summary>
        Task<CardAccess> GetCardAccessAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Access for many cards owned by the same user (one plan load + one unlock query).
        /// Keys are (contentType, contentId).
        /// </summary>
        Task<IReadOnlyDictionary<(ShareContentType ContentType, Guid ContentId), CardAccess>> GetCardAccessBatchAsync(
            Guid ownerId,
            IReadOnlyCollection<(ShareContentType ContentType, Guid ContentId)> cards,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks the card through the owner's active plan when that needs no ticket (an unlimited plan).
        /// Returns whether the card is unlocked afterwards.
        /// </summary>
        Task<bool> TryAutoUnlockAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>What the caller can do on the card the vote belongs to. 404 unless the caller owns the card.</summary>
        Task<AskBackAccessResponse> GetAccessAsync(
            Guid userId,
            Guid voteId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The same as <see cref="GetAccessAsync"/> but for the card itself (poll or check id), so the results screen
        /// can tell whether reasons are hidden before it opens any. 404 unless the caller owns the card.
        /// </summary>
        Task<AskBackAccessResponse> GetOwnedCardAccessAsync(
            Guid userId,
            string? contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unlocks the card the vote belongs to with a plan ticket. Idempotent: an unlocked card is returned as it is.
        /// 403 <c>ASK_BACK_UNLOCK_REQUIRED</c> when the plan cannot unlock it.
        /// </summary>
        Task<AskBackAccessResponse> UnlockAsync(
            Guid userId,
            Guid voteId,
            CancellationToken cancellationToken = default);

        /// <summary>Puts a user on a plan from now. Called by the payment flow (for now, an admin endpoint).</summary>
        Task<ActivatePlanResponse> ActivatePlanAsync(
            Guid userId,
            string? planCode,
            CancellationToken cancellationToken = default);

        /// <summary>Unlocks one card on its own for its owner. Called by the payment flow (for now, an admin endpoint).</summary>
        Task<GrantCardUnlockResponse> GrantCardUnlockAsync(
            Guid userId,
            string? contentType,
            Guid contentId,
            CancellationToken cancellationToken = default);

        /// <summary>The questions per reason a card unlocked on its own gets (the this_card plan's value).</summary>
        Task<int?> GetPurchaseMessageLimitAsync(CancellationToken cancellationToken = default);

        /// <summary>The questions per reason a card with no unlock gets (the free plan's value: none).</summary>
        Task<int?> GetFreeMessageLimitAsync(CancellationToken cancellationToken = default);
    }
}
