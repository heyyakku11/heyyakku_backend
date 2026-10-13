using Microsoft.EntityFrameworkCore;
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

        public async Task<IReadOnlyList<UserSession>> ListLiveByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions
                .Where(session =>
                    session.UserId == userId
                    && session.RevokedAt == null
                    && session.ExpiresAt > utcNow)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<UserSession>> ListByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.UserSessions
                .Where(session => session.UserId == userId)
                .ToListAsync(cancellationToken);
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
