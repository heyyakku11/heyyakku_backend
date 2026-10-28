using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Checks;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Votes;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CheckRepository : ICheckRepository
    {
        private const string OneUsePerUserConstraint = "IX_user_checks_user_id_check_id";
        private const string ShareCodeConstraint = "IX_user_checks_share_code";
        private const string ShareCodeRegistryConstraint = "PK_share_codes";
        private const string OneVotePerItemConstraint = "IX_user_check_votes_user_check_id_voter_id";

        private readonly YakkuDbContext _context;

        public CheckRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<List<Check>> GetForWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default)
        {
            return _context.Checks
                .AsNoTracking()
                .Include(check => check.Options)
                .Where(check => check.IsActive && check.LiveWeekStart == weekStart)
                .OrderBy(check => check.CreatedAt)
                .ThenBy(check => check.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<Check?> GetLiveByIdAsync(Guid id, DateOnly throughWeek, CancellationToken cancellationToken = default)
        {
            return _context.Checks
                .AsNoTracking()
                .Include(check => check.Options)
                .FirstOrDefaultAsync(
                    check => check.Id == id && check.IsActive && check.LiveWeekStart <= throughWeek,
                    cancellationToken);
        }

        public Task<List<Check>> GetAllAsync(DateOnly? weekStart, CancellationToken cancellationToken = default)
        {
            var query = _context.Checks.AsNoTracking().Include(check => check.Options).AsQueryable();
            if (weekStart is not null)
            {
                query = query.Where(check => check.LiveWeekStart == weekStart);
            }

            return query
                .OrderBy(check => check.LiveWeekStart)
                .ThenBy(check => check.CreatedAt)
                .ThenBy(check => check.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<Check?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Checks
                .Include(check => check.Options)
                .FirstOrDefaultAsync(check => check.Id == id, cancellationToken);
        }

        public Task<bool> HasUserChecksAsync(Guid checkId, CancellationToken cancellationToken = default)
        {
            return _context.UserChecks.AnyAsync(item => item.CheckId == checkId, cancellationToken);
        }

        public async Task AddCheckAsync(Check check, CancellationToken cancellationToken = default)
        {
            await _context.Checks.AddAsync(check, cancellationToken);
        }

        public Task RemoveCheckAsync(Check check, CancellationToken cancellationToken = default)
        {
            _context.Checks.Remove(check);
            return Task.CompletedTask;
        }

        public async Task SaveChecksAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Dictionary<Guid, UserCheck>> GetUserChecksByCheckAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserChecks
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .ToDictionaryAsync(item => item.CheckId, cancellationToken);
        }

        public Task<List<UserCheck>> GetUserChecksAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return WithDetails(_context.UserChecks.AsNoTracking())
                .Where(item => item.UserId == userId)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<List<UserCheck>> GetUserChecksPageAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = WithDetails(_context.UserChecks.AsNoTracking())
                .Where(item => item.UserId == userId);

            if (cursorCreatedAt is not null && cursorId is not null)
            {
                query = query.Where(item =>
                    item.CreatedAt < cursorCreatedAt.Value
                    || (item.CreatedAt == cursorCreatedAt.Value && item.Id < cursorId.Value));
            }

            return query
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<UserCheck?> GetOwnedUserCheckForUpdateAsync(
            Guid userCheckId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.UserChecks
                .FirstOrDefaultAsync(item => item.Id == userCheckId && item.UserId == userId, cancellationToken);
        }

        public async Task DeleteUserCheckAsync(UserCheck userCheck, CancellationToken cancellationToken = default)
        {
            _context.UserChecks.Remove(userCheck);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<UserCheck?> GetUserCheckByIdAsync(Guid userCheckId, CancellationToken cancellationToken = default)
        {
            return WithDetails(_context.UserChecks.AsNoTracking())
                .FirstOrDefaultAsync(item => item.Id == userCheckId, cancellationToken);
        }

        public Task<UserCheck?> GetUserCheckByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
        {
            return WithDetails(_context.UserChecks.AsNoTracking())
                .FirstOrDefaultAsync(item => item.ShareCode == shareCode, cancellationToken);
        }

        public async Task<bool> TryAddUserCheckAsync(
            UserCheck userCheck,
            UserCheckVote ownerAnswer,
            Func<CancellationToken, Task>? afterAdded = null,
            CancellationToken cancellationToken = default)
        {
            // One SaveChanges: the check, the maker's answer and the share code registry row are stored together
            // or not at all. The transaction also covers afterAdded (the streak credit), so a check that is not
            // stored earns nothing and a stored one cannot lose its credit. A failed attempt rolls back and the
            // caller retries with a fresh transaction.
            var registry = ShareCode.ForCheck(userCheck);
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.UserChecks.AddAsync(userCheck, cancellationToken);
                await _context.UserCheckVotes.AddAsync(ownerAnswer, cancellationToken);
                await _context.ShareCodes.AddAsync(registry, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                if (afterAdded is not null)
                {
                    await afterAdded(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, OneUsePerUserConstraint))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Detach(userCheck, ownerAnswer, registry);
                throw CheckExceptions.AlreadyUsed();
            }
            catch (DbUpdateException exception) when (
                IsUniqueViolation(exception, ShareCodeConstraint)
                || IsUniqueViolation(exception, ShareCodeRegistryConstraint))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Detach(userCheck, ownerAnswer, registry);
                return false;
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Detach(userCheck, ownerAnswer, registry);
                throw;
            }
        }

        private void Detach(UserCheck userCheck, UserCheckVote vote, ShareCode registry)
        {
            _context.Entry(registry).State = EntityState.Detached;
            _context.Entry(vote).State = EntityState.Detached;
            _context.Entry(userCheck).State = EntityState.Detached;
        }

        public async Task AddVoteAsync(UserCheckVote vote, CancellationToken cancellationToken = default)
        {
            await _context.UserCheckVotes.AddAsync(vote, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, OneVotePerItemConstraint))
            {
                _context.Entry(vote).State = EntityState.Detached;
                throw VoteExceptions.AlreadyVoted();
            }
        }

        private static IQueryable<UserCheck> WithDetails(IQueryable<UserCheck> query)
        {
            return query
                .Include(item => item.Check)
                .ThenInclude(check => check.Options)
                .Include(item => item.Votes);
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres &&
                   postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
