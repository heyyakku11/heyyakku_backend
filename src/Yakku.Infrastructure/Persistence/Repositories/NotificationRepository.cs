using System.Text.Json;
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
                    (notification.ExpiresAt == null || notification.ExpiresAt > now));

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

        public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            await _context.Notifications.AddAsync(notification, cancellationToken);
        }

        public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // Same visibility rule as the list: expired notifications are not shown, so they do not count.
            var now = DateTime.UtcNow;
            return _context.Notifications
                .AsNoTracking()
                .CountAsync(
                    notification => notification.UserId == userId
                                    && !notification.IsRead
                                    && (notification.ExpiresAt == null || notification.ExpiresAt > now),
                    cancellationToken);
        }

        public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.Notifications
                .Where(notification => notification.UserId == userId && !notification.IsRead)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(notification => notification.IsRead, true)
                        .SetProperty(notification => notification.ReadAt, now),
                    cancellationToken);
        }

        public async Task<int> MarkContentReadAsync(
            Guid userId,
            string contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            // Notifications about a card carry { contentType, contentId } in their data.
            var target = JsonSerializer.Serialize(new { contentType, contentId });
            var now = DateTime.UtcNow;
            return await _context.Notifications
                .Where(notification => notification.UserId == userId
                                       && !notification.IsRead
                                       && notification.Data != null
                                       && EF.Functions.JsonContains(notification.Data, target))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(notification => notification.IsRead, true)
                        .SetProperty(notification => notification.ReadAt, now),
                    cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
