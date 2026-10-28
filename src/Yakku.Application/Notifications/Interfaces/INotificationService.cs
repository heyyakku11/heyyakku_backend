using Yakku.Application.Notifications.DTOs;

namespace Yakku.Application.Notifications.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationsPage> GetMineAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);

        /// <summary>One notification, only if it belongs to the user; anyone else gets 404.</summary>
        Task<NotificationResponse> GetAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default);

        /// <summary>How many of the user's notifications are unread (the activity badge).</summary>
        Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Marks all of the user's unread notifications as read; returns how many changed.</summary>
        Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<NotificationResponse> MarkAsReadAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default);
    }
}
