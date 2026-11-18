using Microsoft.EntityFrameworkCore;
using Yakku.Application.AskBack.Interfaces;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserBlockRepository : IUserBlockLookup
    {
        private readonly YakkuDbContext _context;

        public UserBlockRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<bool> IsBlockedAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default)
        {
            return _context.UserBlocks
                .AsNoTracking()
                .AnyAsync(
                    block => block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId,
                    cancellationToken);
        }
    }
}
