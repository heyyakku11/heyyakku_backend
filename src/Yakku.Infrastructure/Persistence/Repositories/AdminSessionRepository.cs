using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AdminSessionRepository : IAdminSessionRepository
    {
        private readonly YakkuDbContext _context;

        public AdminSessionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AdminSession session, CancellationToken cancellationToken = default)
        {
            await _context.AdminSessions.AddAsync(session, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task<AdminSession?> FindByHashAsync(string hash, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return _context.AdminSessions
                .AsNoTracking()
                .Include(session => session.Admin)
                .FirstOrDefaultAsync(
                    session => session.SessionHash == hash
                        || (session.PreviousSessionHash == hash && session.PreviousHashValidUntil > utcNow),
                    cancellationToken);
        }

        public async Task<bool> TryRotateAsync(
            Guid sessionId,
            string expectedHash,
            string newHash,
            DateTime utcNow,
            DateTime graceUntil,
            DateTime idleExpiresAt,
            CancellationToken cancellationToken = default)
        {
            // One conditional UPDATE: only the request that still sees the old hash wins, and a revoked row never rotates.
            var rows = await _context.AdminSessions
                .Where(session => session.Id == sessionId
                    && session.SessionHash == expectedHash
                    && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(session => session.PreviousSessionHash, session => session.SessionHash)
                        .SetProperty(session => session.PreviousHashValidUntil, graceUntil)
                        .SetProperty(session => session.SessionHash, newHash)
                        .SetProperty(session => session.RotatedAt, utcNow)
                        .SetProperty(session => session.LastActivityAt, utcNow)
                        .SetProperty(session => session.IdleExpiresAt, idleExpiresAt),
                    cancellationToken);

            return rows == 1;
        }

        public Task TouchAsync(Guid sessionId, DateTime utcNow, DateTime idleExpiresAt, CancellationToken cancellationToken = default)
        {
            return _context.AdminSessions
                .Where(session => session.Id == sessionId && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(session => session.LastActivityAt, utcNow)
                        .SetProperty(session => session.IdleExpiresAt, idleExpiresAt),
                    cancellationToken);
        }

        public async Task<bool> RevokeAsync(Guid adminId, Guid sessionId, string reason, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var rows = await _context.AdminSessions
                .Where(session => session.Id == sessionId && session.AdminId == adminId && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(session => session.RevokedAt, utcNow)
                        .SetProperty(session => session.RevocationReason, reason)
                        .SetProperty(session => session.PreviousSessionHash, (string?)null)
                        .SetProperty(session => session.PreviousHashValidUntil, (DateTime?)null),
                    cancellationToken);

            return rows == 1;
        }

        public Task<int> RevokeAllAsync(Guid adminId, string reason, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return _context.AdminSessions
                .Where(session => session.AdminId == adminId && session.RevokedAt == null)
                .ExecuteUpdateAsync(
                    update => update
                        .SetProperty(session => session.RevokedAt, utcNow)
                        .SetProperty(session => session.RevocationReason, reason)
                        .SetProperty(session => session.PreviousSessionHash, (string?)null)
                        .SetProperty(session => session.PreviousHashValidUntil, (DateTime?)null),
                    cancellationToken);
        }

        public Task<int> DeleteEndedBeforeAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        {
            return _context.AdminSessions
                .Where(session => session.AbsoluteExpiresAt < cutoff || session.RevokedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
