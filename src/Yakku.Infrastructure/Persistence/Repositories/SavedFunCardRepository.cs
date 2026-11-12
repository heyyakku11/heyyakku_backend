using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Checks.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class SavedFunCardRepository : ISavedFunCardRepository
    {
        private readonly YakkuDbContext _context;

        public SavedFunCardRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<Check?> GetActiveFunCardAsync(Guid checkId, CancellationToken cancellationToken = default)
        {
            return _context.Checks
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    check => check.Id == checkId && check.Kind == CheckKind.Fun && check.IsActive,
                    cancellationToken);
        }

        public Task<SavedFunCard?> GetAsync(Guid userId, Guid checkId, CancellationToken cancellationToken = default)
        {
            return _context.SavedFunCards
                .FirstOrDefaultAsync(saved => saved.UserId == userId && saved.CheckId == checkId, cancellationToken);
        }

        public async Task AddAsync(SavedFunCard saved, CancellationToken cancellationToken = default)
        {
            await _context.SavedFunCards.AddAsync(saved, cancellationToken);
        }

        public void Remove(SavedFunCard saved)
        {
            _context.SavedFunCards.Remove(saved);
        }

        public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation,
                    ConstraintName: "IX_SavedFunCards_UserId_CheckId"
                })
            {
                foreach (var entry in _context.ChangeTracker.Entries<SavedFunCard>()
                    .Where(item => item.State == EntityState.Added)
                    .ToList())
                {
                    entry.State = EntityState.Detached;
                }

                return false;
            }
        }

        public async Task<IReadOnlyList<SavedFunCard>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorCheckId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.SavedFunCards
                .AsNoTracking()
                .Include(saved => saved.Check).ThenInclude(check => check.Options)
                .Include(saved => saved.Check).ThenInclude(check => check.CardTheme)
                .Where(saved => saved.UserId == userId && saved.Check.Kind == CheckKind.Fun && saved.Check.IsActive);

            if (cursorSavedAt is not null && cursorCheckId is not null)
            {
                query = query.Where(saved =>
                    saved.SavedAt < cursorSavedAt.Value
                    || (saved.SavedAt == cursorSavedAt.Value && saved.CheckId < cursorCheckId.Value));
            }

            return await query
                .OrderByDescending(saved => saved.SavedAt)
                .ThenByDescending(saved => saved.CheckId)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
    }
}
