using Yakku.Domain.Entities;

namespace Yakku.Application.Personality.Interfaces
{
    public interface IPersonalityRepository
    {
        Task<List<PersonalityTrait>> GetActiveTraitsAsync(CancellationToken cancellationToken = default);

        /// <summary>The traits with these ids, active or not (the caller checks).</summary>
        Task<List<PersonalityTrait>> GetTraitsByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default);

        /// <summary>Creates the user's card if it is missing. Safe to call repeatedly and concurrently.</summary>
        Task EnsureCardAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>The user's card with its self traits, read-only.</summary>
        Task<PersonalityCard?> GetCardByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>The card with this share code, with its owner's profile and self traits, read-only.</summary>
        Task<PersonalityCard?> GetCardByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

        Task<bool> HasVotedAsync(Guid cardId, Guid voterUserId, CancellationToken cancellationToken = default);

        /// <summary>Per-trait vote counts across all votes of a card, computed from the persisted vote rows.</summary>
        Task<List<TraitTally>> GetTraitTalliesAsync(Guid cardId, CancellationToken cancellationToken = default);

        /// <summary>Runs <paramref name="action"/> in one database transaction; rolls back if it throws.</summary>
        Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        /// <summary>Loads the user's card for update and locks its row until the transaction ends. Needs a transaction.</summary>
        Task<PersonalityCard?> LockCardByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Loads the card with this share code for update and locks its row until the transaction ends. Needs a transaction.</summary>
        Task<PersonalityCard?> LockCardByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

        void AddVote(PersonalityVote vote);

        /// <summary>Saves changes; a violated one-vote-per-card constraint becomes an "already voted" error.</summary>
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
