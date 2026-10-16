using Microsoft.EntityFrameworkCore;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class GuestRepository : IGuestRepository
    {
        private readonly YakkuDbContext _context;

        public GuestRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Guest guest, CancellationToken cancellationToken = default)
        {
            await _context.Guests.AddAsync(guest, cancellationToken);
        }

        public async Task<Guest?> GetByTokenHashAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            return await _context.Guests
                .FirstOrDefaultAsync(guest => guest.GuestTokenHash == tokenHash, cancellationToken);
        }

        public async Task<Guest?> GetByTokenHashForUpdateAsync(
            string tokenHash,
            CancellationToken cancellationToken = default)
        {
            // FOR UPDATE blocks a concurrent claim of the same guest until this transaction ends. Once the
            // winner commits, the loser re-evaluates the query and finds no row.
            var matches = await _context.Guests
                .FromSqlInterpolated(
                    $"SELECT * FROM guests WHERE \"GuestTokenHash\" = {tokenHash} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return matches.FirstOrDefault();
        }

        public async Task<IReadOnlyList<Guid>> ListExpiredIdsAsync(
            DateTime expiredBeforeUtc,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return await _context.Guests
                .AsNoTracking()
                .Where(guest => guest.ExpiresAt <= expiredBeforeUtc)
                .OrderBy(guest => guest.ExpiresAt)
                .Select(guest => guest.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> DeleteExpiredByIdAsync(
            Guid guestId,
            DateTime expiredBeforeUtc,
            CancellationToken cancellationToken = default)
        {
            var deleted = await _context.Guests
                .Where(guest => guest.Id == guestId && guest.ExpiresAt <= expiredBeforeUtc)
                .ExecuteDeleteAsync(cancellationToken);
            return deleted == 1;
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

        public async Task<IReadOnlyList<Guest>> ListAsync(
            bool? active,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Guests.AsNoTracking().AsQueryable();
            if (active == true)
            {
                query = query.Where(guest => guest.ExpiresAt > utcNow);
            }
            else if (active == false)
            {
                query = query.Where(guest => guest.ExpiresAt <= utcNow);
            }

            return await query
                .OrderByDescending(guest => guest.CreatedAt)
                .ThenByDescending(guest => guest.Id)
                .ToListAsync(cancellationToken);
        }

        public void Delete(Guest guest)
        {
            _context.Guests.Remove(guest);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
