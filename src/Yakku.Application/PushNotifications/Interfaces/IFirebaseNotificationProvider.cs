using Yakku.Application.PushNotifications.DTOs;

namespace Yakku.Application.PushNotifications.Interfaces
{
    public interface IFirebaseNotificationProvider
    {
        Task<FirebaseTokenSendResult> SendAsync(
            string pushToken,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<FirebaseTokenSendResult>> SendToManyAsync(
            IReadOnlyList<string> pushTokens,
            NotificationPayload payload,
            CancellationToken cancellationToken = default);
    }
}
