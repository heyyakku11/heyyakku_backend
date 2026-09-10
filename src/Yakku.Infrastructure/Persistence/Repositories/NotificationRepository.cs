using Microsoft.EntityFrameworkCore;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class NotificationRepository : INotificationRepository
    {
        private readonly YakkuDbContext _context;

        public NotificationRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Notification>> GetByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var query = _context.Notifications
                .AsNoTracking()
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.ExpiresAt > now);

            if (cursorCreatedAt is not null && cursorId is not null)
            {
                query = query.Where(notification =>
                    notification.CreatedAt < cursorCreatedAt.Value
                    || (notification.CreatedAt == cursorCreatedAt.Value && notification.Id < cursorId.Value));
            }

            return await query
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<Notification?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.Notifications.FirstOrDefaultAsync(
                notification => notification.Id == id && notification.UserId == userId,
                cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
