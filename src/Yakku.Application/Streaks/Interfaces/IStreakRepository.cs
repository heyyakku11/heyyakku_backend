namespace Yakku.Application.Streaks.Interfaces
{
    public interface IStreakRepository
    {
        /// <summary>Runs the action in one transaction, joining an ambient one (for example the poll's).</summary>
        Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Records the day if the user has none for it yet. The unique (user, date) index decides, so concurrent and
        /// repeated calls produce exactly one row. Returns true only for the call that created it.
        /// </summary>
        Task<bool> TryAddDayAsync(
            Guid userId,
            DateOnly date,
            DateTime firstActivityAtUtc,
            CancellationToken cancellationToken = default);

        /// <summary>Reads the summary row and locks it until the ambient transaction ends. Null if there is none.</summary>
        Task<StreakState?> LockSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Creates the summary if none exists. Returns false if another writer created it first.</summary>
        Task<bool> TryCreateSummaryAsync(
            Guid userId,
            StreakState state,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task UpdateSummaryAsync(
            Guid userId,
            StreakState state,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Recomputes the summary from the day history in one statement and returns it (null when the user has no
        /// activity days).
        /// </summary>
        Task<StreakState?> RebuildSummaryAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        Task<StreakState?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<DateOnly>> GetDaysAsync(
            Guid userId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default);
    }
}
