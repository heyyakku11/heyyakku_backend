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
        }

        public async Task<AdminSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.AdminSessions.FirstOrDefaultAsync(session => session.Id == id, cancellationToken);
        }

        public Task DeleteAsync(AdminSession session, CancellationToken cancellationToken = default)
        {
            _context.AdminSessions.Remove(session);
            return Task.CompletedTask;
        }

        public async Task DeleteAllByAdminIdAsync(Guid adminId, CancellationToken cancellationToken = default)
        {
            var sessions = await _context.AdminSessions
                .Where(session => session.AdminId == adminId)
                .ToListAsync(cancellationToken);
            _context.AdminSessions.RemoveRange(sessions);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
