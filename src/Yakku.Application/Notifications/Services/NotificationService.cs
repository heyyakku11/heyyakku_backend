using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Notifications.Mapper;

namespace Yakku.Application.Notifications.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notifications;

        public NotificationService(INotificationRepository notifications)
        {
            _notifications = notifications;
        }

        public async Task<NotificationsPage> GetMineAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = NotificationCursor.TryDecode(cursor);
            var take = NotificationCursor.PageSize + 1;
            var notifications = await _notifications.GetByUserAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = notifications.Count > NotificationCursor.PageSize;
            var items = notifications
                .Take(NotificationCursor.PageSize)
                .Select(notification => notification.ToResponse())
                .ToList();

            string? nextCursor = null;
            if (hasMore)
            {
                var last = notifications[NotificationCursor.PageSize - 1];
                nextCursor = NotificationCursor.Encode(last.CreatedAt, last.Id);
            }

            return new NotificationsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(NotificationCursor.PageSize, nextCursor, hasMore)
            };
        }

        public async Task<NotificationResponse> MarkAsReadAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default)
        {
            var notification = await _notifications.GetByIdForUserAsync(
                notificationId,
                userId,
                cancellationToken);

            if (notification is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Notification not found");
            }

            notification.MarkAsRead();
            await _notifications.SaveChangesAsync(cancellationToken);
            return notification.ToResponse();
        }
    }
}
