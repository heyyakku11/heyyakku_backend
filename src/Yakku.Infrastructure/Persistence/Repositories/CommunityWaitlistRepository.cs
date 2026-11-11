using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Community.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CommunityWaitlistRepository : ICommunityWaitlistRepository
    {
        private readonly YakkuDbContext _context;

        public CommunityWaitlistRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<(CommunityWaitlistEntry Entry, bool Added)> AddIfMissingAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.CommunityWaitlist
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.UserId == userId, cancellationToken);
            if (existing is not null)
            {
                return (existing, false);
            }

            var created = CommunityWaitlistEntry.Create(userId);
            _context.CommunityWaitlist.Add(created);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return (created, true);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // A concurrent request queued the same user first.
                _context.Entry(created).State = EntityState.Detached;
                var winner = await _context.CommunityWaitlist
                    .AsNoTracking()
                    .FirstAsync(entry => entry.UserId == userId, cancellationToken);
                return (winner, false);
            }
        }
    }
}
