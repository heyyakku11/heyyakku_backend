using Microsoft.EntityFrameworkCore;
using Yakku.Application.Streaks;
using Yakku.Application.Streaks.Interfaces;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Streak persistence. Writes are raw PostgreSQL statements so the database, not the application, decides races:
    /// the (user, day) primary key makes the daily row idempotent and the summary row is locked with FOR UPDATE.
    /// </summary>
    public class StreakRepository : IStreakRepository
    {
        private readonly YakkuDbContext _context;

        public StreakRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action(cancellationToken);
                return;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<bool> TryAddDayAsync(
            Guid userId,
            DateOnly date,
            DateTime firstActivityAtUtc,
            CancellationToken cancellationToken = default)
        {
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO user_activity_days (user_id, activity_date, first_activity_at, created_at)
                VALUES ({userId}, {date}, {firstActivityAtUtc}, now())
                ON CONFLICT (user_id, activity_date) DO NOTHING
                """,
                cancellationToken);
            return inserted > 0;
        }

        public async Task<StreakState?> LockSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var rows = await _context.UserStreaks
                .FromSqlInterpolated($"SELECT * FROM user_streaks WHERE user_id = {userId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            return rows.Count == 0 ? null : ToState(rows[0]);
        }

        public async Task<bool> TryCreateSummaryAsync(
            Guid userId,
            StreakState state,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var inserted = await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO user_streaks (user_id, current_streak, longest_streak, last_activity_date, updated_at)
                VALUES ({userId}, {state.Current}, {state.Longest}, {state.LastActivityDate}, {utcNow})
                ON CONFLICT (user_id) DO NOTHING
                """,
                cancellationToken);
            return inserted > 0;
        }

        public async Task UpdateSummaryAsync(
            Guid userId,
            StreakState state,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE user_streaks
                SET current_streak = {state.Current},
                    longest_streak = {state.Longest},
                    last_activity_date = {state.LastActivityDate},
                    updated_at = {utcNow}
                WHERE user_id = {userId}
                """,
                cancellationToken);
        }

        public async Task<StreakState?> RebuildSummaryAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            // Gaps and islands: consecutive dates share (date - row_number). The current streak is the island that
            // ends on the latest day; the longest is the longest island.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                WITH days AS (
                    SELECT activity_date,
                           activity_date - (row_number() OVER (ORDER BY activity_date))::int AS grp
                    FROM user_activity_days
                    WHERE user_id = {userId}
                ),
                islands AS (
                    SELECT grp, COUNT(*)::int AS len, MAX(activity_date) AS last_date
                    FROM days
                    GROUP BY grp
                ),
                agg AS (
                    SELECT MAX(len) AS longest, MAX(last_date) AS last_date FROM islands
                )
                INSERT INTO user_streaks (user_id, current_streak, longest_streak, last_activity_date, updated_at)
                SELECT {userId}, i.len, agg.longest, agg.last_date, {utcNow}
                FROM agg
                JOIN islands i ON i.last_date = agg.last_date
                ON CONFLICT (user_id) DO UPDATE
                SET current_streak = EXCLUDED.current_streak,
                    longest_streak = EXCLUDED.longest_streak,
                    last_activity_date = EXCLUDED.last_activity_date,
                    updated_at = EXCLUDED.updated_at
                """,
                cancellationToken);

            return await GetSummaryAsync(userId, cancellationToken);
        }

        public async Task<StreakState?> GetSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var row = await _context.UserStreaks
                .AsNoTracking()
                .Where(streak => streak.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            return row is null ? null : ToState(row);
        }

        public async Task<IReadOnlyList<DateOnly>> GetDaysAsync(
            Guid userId,
            DateOnly from,
            DateOnly to,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserActivityDays
                .AsNoTracking()
                .Where(day => day.UserId == userId && day.ActivityDate >= from && day.ActivityDate <= to)
                .OrderBy(day => day.ActivityDate)
                .Select(day => day.ActivityDate)
                .ToListAsync(cancellationToken);
        }

        private static StreakState ToState(Yakku.Domain.Entities.UserStreak row)
        {
            return new StreakState(row.CurrentStreak, row.LongestStreak, row.LastActivityDate);
        }
    }
}
