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
                .FirstOrDefaultAsync(guest => guest.TokenHash == tokenHash, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
