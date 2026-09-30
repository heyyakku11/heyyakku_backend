using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class SavedPollRepository : ISavedPollRepository
    {
        private readonly YakkuDbContext _context;

        public SavedPollRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<SavedPoll?> GetByUserAndPollAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return await _context.SavedPolls
                .FirstOrDefaultAsync(
                    saved => saved.UserId == userId && saved.PollId == pollId,
                    cancellationToken);
        }

        public async Task AddAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default)
        {
            await _context.SavedPolls.AddAsync(savedPoll, cancellationToken);
        }

        public Task DeleteAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default)
        {
            _context.SavedPolls.Remove(savedPoll);
            return Task.CompletedTask;
        }

        public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_SavedPolls_UserId_PollId"))
            {
                foreach (var entry in _context.ChangeTracker.Entries<SavedPoll>()
                    .Where(item => item.State == EntityState.Added)
                    .ToList())
                {
                    entry.State = EntityState.Detached;
                }

                return false;
            }
        }

        public async Task<IReadOnlyList<SavedPoll>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.SavedPolls
                .AsNoTracking()
                .Include(saved => saved.Poll)
                .ThenInclude(poll => poll.Options)
                .ThenInclude(option => option.Image)
                .Where(saved => saved.UserId == userId && saved.Poll.Status != PollStatus.Deleted);

            if (cursorSavedAt is not null && cursorPollId is not null)
            {
                query = query.Where(saved =>
                    saved.SavedAt < cursorSavedAt.Value
                    || (saved.SavedAt == cursorSavedAt.Value && saved.PollId < cursorPollId.Value));
            }

            return await query
                .OrderByDescending(saved => saved.SavedAt)
                .ThenByDescending(saved => saved.PollId)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres &&
                   postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
