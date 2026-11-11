using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Checks.Themes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CheckThemeRepository : ICheckThemeRepository
    {
        private readonly YakkuDbContext _context;

        public CheckThemeRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<List<CardTheme>> GetAllTypesAsync(CancellationToken cancellationToken = default)
        {
            return GetByKindAsync(CardThemeKind.Check, cancellationToken);
        }

        public Task<List<CardTheme>> GetByKindAsync(CardThemeKind kind, CancellationToken cancellationToken = default)
        {
            return _context.CardThemes
                .AsNoTracking()
                .Where(theme => theme.Kind == kind)
                .OrderBy(theme => theme.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<CardTheme?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            // Check themes only: a weekly check cannot be styled with another kind of theme.
            return _context.CardThemes
                .AsNoTracking()
                .FirstOrDefaultAsync(theme => theme.Id == id && theme.Kind == CardThemeKind.Check, cancellationToken);
        }

        public async Task<bool> TryAddTypeAsync(CardTheme type, CancellationToken cancellationToken = default)
        {
            var lowered = type.Name.ToLower();
            if (await _context.CardThemes.AnyAsync(
                    existing => existing.Kind == type.Kind && existing.Name.ToLower() == lowered,
                    cancellationToken))
            {
                return false;
            }

            _context.CardThemes.Add(type);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                _context.Entry(type).State = EntityState.Detached;
                return false;
            }
        }
    }
}
