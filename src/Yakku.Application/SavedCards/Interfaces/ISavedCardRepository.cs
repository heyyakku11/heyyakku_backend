using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.SavedCards.Interfaces
{
    /// <summary>Storage of the cards users saved (polls and fun cards) in the single saved_cards table.</summary>
    public interface ISavedCardRepository
    {
        /// <summary>An active just-for-fun card by id; null for anything else (weekly checks included).</summary>
        Task<Check?> GetActiveFunCardAsync(Guid checkId, CancellationToken cancellationToken = default);

        Task<SavedCard?> GetAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default);

        /// <summary>The ids of the user's saved cards of one kind.</summary>
        Task<HashSet<Guid>> GetSavedCardIdsAsync(
            Guid userId,
            SavedCardType cardType,
            CancellationToken cancellationToken = default);

        Task AddAsync(SavedCard saved, CancellationToken cancellationToken = default);

        void Remove(SavedCard saved);

        /// <summary>False when a concurrent save of the same card won (treated as already saved).</summary>
        Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// The user's saved cards of every kind, most recently saved first, with the poll or fun card loaded.
        /// Fun cards that were deactivated or removed are left out.
        /// </summary>
        Task<IReadOnlyList<SavedCard>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default);
    }
}
