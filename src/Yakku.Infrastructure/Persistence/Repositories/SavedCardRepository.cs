using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.SavedCards.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence.Configurations;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class SavedCardRepository : ISavedCardRepository
    {
        private readonly YakkuDbContext _context;

        public SavedCardRepository(YakkuDbContext context)
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

        public Task<SavedCard?> GetAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default)
        {
            return cardType == SavedCardType.Poll
                ? _context.SavedCards.FirstOrDefaultAsync(
                    saved => saved.UserId == userId && saved.CardType == SavedCardType.Poll && saved.PollId == cardId,
                    cancellationToken)
                : _context.SavedCards.FirstOrDefaultAsync(
                    saved => saved.UserId == userId && saved.CardType == SavedCardType.FunCard && saved.FunCardId == cardId,
                    cancellationToken);
        }

        public async Task<HashSet<Guid>> GetSavedCardIdsAsync(
            Guid userId,
            SavedCardType cardType,
            CancellationToken cancellationToken = default)
        {
            var query = _context.SavedCards.AsNoTracking().Where(saved => saved.UserId == userId && saved.CardType == cardType);
            var ids = cardType == SavedCardType.Poll
                ? await query.Select(saved => saved.PollId!.Value).ToListAsync(cancellationToken)
                : await query.Select(saved => saved.FunCardId!.Value).ToListAsync(cancellationToken);
            return ids.ToHashSet();
        }

        public async Task AddAsync(SavedCard saved, CancellationToken cancellationToken = default)
        {
            await _context.SavedCards.AddAsync(saved, cancellationToken);
        }

        public void Remove(SavedCard saved)
        {
            _context.SavedCards.Remove(saved);
        }

        public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (IsDuplicateSave(exception))
            {
                foreach (var entry in _context.ChangeTracker.Entries<SavedCard>()
                    .Where(item => item.State == EntityState.Added)
                    .ToList())
                {
                    entry.State = EntityState.Detached;
                }

                return false;
            }
        }

        public async Task<IReadOnlyList<SavedCard>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var query = _context.SavedCards
                .AsNoTracking()
                .Include(saved => saved.Poll).ThenInclude(poll => poll!.Options)
                .Include(saved => saved.Check).ThenInclude(check => check!.Options)
                .Where(saved => saved.UserId == userId
                    && (saved.CardType == SavedCardType.Poll
                        || (saved.Check != null && saved.Check.Kind == CheckKind.Fun && saved.Check.IsActive)));

            if (cursorSavedAt is not null && cursorId is not null)
            {
                query = query.Where(saved =>
                    saved.SavedAt < cursorSavedAt.Value
                    || (saved.SavedAt == cursorSavedAt.Value && saved.Id < cursorId.Value));
            }

            return await query
                .OrderByDescending(saved => saved.SavedAt)
                .ThenByDescending(saved => saved.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        private static bool IsDuplicateSave(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && (string.Equals(postgres.ConstraintName, SavedCardConfiguration.PollUniqueIndex, StringComparison.Ordinal)
                    || string.Equals(postgres.ConstraintName, SavedCardConfiguration.FunCardUniqueIndex, StringComparison.Ordinal));
        }
    }
}
