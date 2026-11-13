using Yakku.Application.SavedCards.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.SavedCards.Interfaces
{
    /// <summary>A user's saved cards: polls from other users and fun cards, in one list.</summary>
    public interface ISavedCardService
    {
        /// <summary>
        /// Saves a card. Idempotent. A poll must be someone else's (400 for your own, 404 when missing);
        /// a fun card must be active (404 otherwise, weekly checks included).
        /// </summary>
        Task<SavedCardStateResponse> SaveAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default);

        /// <summary>Removes a card from the saved list. Idempotent.</summary>
        Task<SavedCardStateResponse> UnsaveAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default);

        /// <summary>One page of the saved cards of every kind, most recently saved first.</summary>
        Task<SavedCardsPage> ListAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);
    }
}
