using Yakku.Domain.Entities;

namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestRepository
    {
        Task AddAsync(Guest guest, CancellationToken cancellationToken = default);
        Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the guest and takes a row lock until the surrounding transaction ends, so two claims of the
        /// same guest are serialised. Must be called inside <see cref="ExecuteInTransactionAsync"/>.
        /// </summary>
        Task<Guest?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Guest>> ListAsync(bool? active, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Ids of guests whose <c>ExpiresAt</c> is at or before the cutoff, oldest first.</summary>
        Task<IReadOnlyList<Guid>> ListExpiredIdsAsync(
            DateTime expiredBeforeUtc,
            int batchSize,
            CancellationToken cancellationToken = default);

        /// <summary>Deletes the guest only if it is still expired at the cutoff. Returns false when nothing was deleted.</summary>
        Task<bool> DeleteExpiredByIdAsync(
            Guid guestId,
            DateTime expiredBeforeUtc,
            CancellationToken cancellationToken = default);

        void Delete(Guest guest);

        /// <summary>
        /// Runs <paramref name="action"/> in one database transaction. Every repository resolved from the same
        /// scope joins it. Any exception rolls the whole unit back and is rethrown.
        /// </summary>
        Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
