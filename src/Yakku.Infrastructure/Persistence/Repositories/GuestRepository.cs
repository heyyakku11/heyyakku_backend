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

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
