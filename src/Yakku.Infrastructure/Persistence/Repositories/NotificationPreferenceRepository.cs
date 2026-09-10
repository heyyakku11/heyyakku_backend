using Microsoft.EntityFrameworkCore;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class NotificationPreferenceRepository : INotificationPreferenceRepository
    {
        private readonly YakkuDbContext _context;

        public NotificationPreferenceRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<NotificationPreference?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.NotificationPreferences.FirstOrDefaultAsync(
                preference => preference.UserId == userId,
                cancellationToken);
        }

        public async Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
        {
            await _context.NotificationPreferences.AddAsync(preference, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
