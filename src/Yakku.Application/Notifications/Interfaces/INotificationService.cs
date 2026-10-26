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

        Task<NotificationResponse> MarkAsReadAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default);
    }
}
