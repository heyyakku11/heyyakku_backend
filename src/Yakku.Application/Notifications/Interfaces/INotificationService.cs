using Yakku.Application.Notifications.DTOs;

namespace Yakku.Application.Notifications.Interfaces
{
    public interface INotificationService
    {
        Task<NotificationsPage> GetMineAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);

        Task<NotificationResponse> MarkAsReadAsync(
            Guid userId,
            Guid notificationId,
            CancellationToken cancellationToken = default);
    }
}
