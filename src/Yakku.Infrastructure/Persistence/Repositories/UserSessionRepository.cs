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

        public Task<bool> HasLiveSessionForDeviceAsync(
            Guid deviceId,
            DateTime utcNow,
            Guid? exceptSessionId,
            CancellationToken cancellationToken = default)
        {
            return _context.UserSessions.AnyAsync(
                session => session.DeviceId == deviceId
                    && session.RevokedAt == null
                    && session.ExpiresAt > utcNow
                    && (exceptSessionId == null || session.Id != exceptSessionId),
                cancellationToken);
        }

        public Task<bool> IsLiveAsync(
            Guid sessionId,
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return _context.UserSessions.AsNoTracking().AnyAsync(
                session => session.Id == sessionId
                    && session.UserId == userId
                    && session.RevokedAt == null
                    && session.ExpiresAt > utcNow,
                cancellationToken);
        }

        public async Task LockUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM users WHERE \"Id\" = {userId} FOR UPDATE",
                cancellationToken);
        }

        public async Task<bool> TryRotateAsync(
            Guid sessionId,
            string expectedHash,
            string newHash,
            DateTime newExpiresAt,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var rotated = await _context.UserSessions
                .Where(session => session.Id == sessionId
                    && session.RefreshTokenHash == expectedHash
                    && session.RevokedAt == null
                    && session.ExpiresAt > utcNow)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(session => session.RefreshTokenHash, newHash)
                        .SetProperty(session => session.ExpiresAt, newExpiresAt)
                        .SetProperty(session => session.LastActiveAt, utcNow),
                    cancellationToken);

            // The tracked copy (if any) is stale now.
            foreach (var entry in _context.ChangeTracker.Entries<UserSession>()
                         .Where(entry => entry.Entity.Id == sessionId && entry.State == EntityState.Unchanged)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return rotated > 0;
        }

        public Task<int> DeleteEndedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
        {
            return _context.UserSessions
                .Where(session => (session.RevokedAt != null && session.RevokedAt < cutoffUtc)
                    || session.ExpiresAt < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
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
