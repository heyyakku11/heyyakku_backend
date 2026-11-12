using Yakku.Application.Checks.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Interfaces
{
    public interface ISavedFunCardRepository
    {
        /// <summary>An active just-for-fun card by id; null for anything else (weekly checks included).</summary>
        Task<Check?> GetActiveFunCardAsync(Guid checkId, CancellationToken cancellationToken = default);

        Task<SavedFunCard?> GetAsync(Guid userId, Guid checkId, CancellationToken cancellationToken = default);

        Task AddAsync(SavedFunCard saved, CancellationToken cancellationToken = default);

        void Remove(SavedFunCard saved);

        /// <summary>False when a concurrent save of the same card won (treated as already saved).</summary>
        Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SavedFunCard>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorCheckId,
            int take,
            CancellationToken cancellationToken = default);
    }

    /// <summary>A user's saved just-for-fun cards. Weekly checks and personality cards cannot be saved.</summary>
    public interface ISavedFunCardService
    {
        Task<SavedFunCardStateResponse> SaveAsync(Guid userId, Guid funCardId, CancellationToken cancellationToken = default);

        Task<SavedFunCardStateResponse> UnsaveAsync(Guid userId, Guid funCardId, CancellationToken cancellationToken = default);

        Task<SavedFunCardsPage> ListAsync(Guid userId, string? cursor, CancellationToken cancellationToken = default);
    }
}
