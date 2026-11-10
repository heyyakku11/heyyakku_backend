using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Checks.Themes;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CheckThemeRepository : ICheckThemeRepository
    {
        private readonly YakkuDbContext _context;

        public CheckThemeRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<List<CheckType>> GetAllTypesAsync(CancellationToken cancellationToken = default)
        {
            return _context.CheckTypes
                .AsNoTracking()
                .OrderBy(type => type.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<CheckType?> GetTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.CheckTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(type => type.Id == id, cancellationToken);
        }

        public async Task<bool> TryAddTypeAsync(CheckType type, CancellationToken cancellationToken = default)
        {
            var lowered = type.Name.ToLower();
            if (await _context.CheckTypes.AnyAsync(existing => existing.Name.ToLower() == lowered, cancellationToken))
            {
                return false;
            }

            _context.CheckTypes.Add(type);
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
