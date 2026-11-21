using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Notifications.Mapper;

namespace Yakku.Application.Notifications.Services
{
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notifications;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public NotificationService(
            INotificationRepository notifications,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _notifications = notifications;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
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

        public async Task<NotificationResponse> GetAsync(
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

            return notification.ToResponse();
        }

        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.NotifUnread(userId),
                _cacheOptions.NotifUnreadSoft,
                _cacheOptions.NotifUnreadHard,
                ct => _notifications.CountUnreadAsync(userId, ct),
                cancellationToken);
        }

        public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var count = await _notifications.MarkAllReadAsync(userId, cancellationToken);
            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterNotificationChange(userId), cancellationToken);
            return count;
        }

        public async Task<int> MarkContentAsReadAsync(
            Guid userId,
            string contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            var normalized = contentType.Trim().ToLowerInvariant();
            if (normalized is not ("poll" or "check"))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Content type must be poll or check.",
                    "contentType");
            }

            var count = await _notifications.MarkContentReadAsync(userId, normalized, contentId, cancellationToken);
            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterNotificationChange(userId), cancellationToken);
            return count;
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
            await _cache.RemoveManyAsync(ResponseCacheKeys.AfterNotificationChange(userId), cancellationToken);
            return notification.ToResponse();
        }
    }
}
