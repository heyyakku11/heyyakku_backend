using Yakku.Application.Auth.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserSessionRepository : IUserSessionRepository
    {
        private readonly YakkuDbContext _context;

        public UserSessionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            await _context.UserSessions.AddAsync(session, cancellationToken);
        }

        public async Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions.FindAsync([id], cancellationToken);
        }

        public Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            _context.UserSessions.Remove(session);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
