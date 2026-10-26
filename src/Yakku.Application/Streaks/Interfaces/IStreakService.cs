using Yakku.Application.Streaks.DTOs;

namespace Yakku.Application.Streaks.Interfaces
{
    public sealed record StreakCreditResult(
        bool Credited,
        int CurrentStreak,
        int LongestStreak,
        DateOnly ActivityDate);

    public interface IStreakService
    {
        /// <summary>
        /// Counts a qualifying activity for a user. Call it from inside the transaction of the business operation
        /// (it joins an ambient transaction), only for an operation that has just succeeded, with the user id taken
        /// from the authenticated identity. Idempotent per user and UTC day: it credits a day at most once.
        /// </summary>
        Task<StreakCreditResult> RecordAsync(
            Guid userId,
            StreakActivity activity,
            DateTime occurredAtUtc,
            CancellationToken cancellationToken = default);

        /// <summary>The user's streak as of now, with an expired streak shown as zero.</summary>
        Task<StreakResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Active days in a bounded range (at most 366 days; defaults to the last 30).</summary>
        Task<StreakHistoryResponse> GetHistoryAsync(
            Guid userId,
            DateOnly? from,
            DateOnly? to,
            CancellationToken cancellationToken = default);

        /// <summary>Recomputes the summary from the day history. Safe to repeat.</summary>
        Task RebuildAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
