using Yakku.Application.PushNotifications.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.PushNotifications.Interfaces
{
    public interface IPushNotificationService
    {
        Task<PushDeviceNotificationResult> SendToDeviceAsync(
            Device device,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);

        Task<PushUserNotificationResult> SendToDevicesAsync(
            IReadOnlyList<Device> devices,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);

        Task<PushDeviceNotificationResult> SendToDeviceAsync(
            string installationId,
            string platform,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);

        Task<PushUserNotificationResult> SendToUserAsync(
            Guid userId,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);

        Task<PushDeviceNotificationResult> SendTestToDeviceAsync(
            TestDeviceNotificationRequest request,
            CancellationToken cancellationToken = default);

        Task<PushUserNotificationResult> SendTestToUserAsync(
            TestUserNotificationRequest request,
            CancellationToken cancellationToken = default);
    }
}
