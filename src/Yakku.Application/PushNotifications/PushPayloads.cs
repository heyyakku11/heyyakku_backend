using Yakku.Application.PushNotifications.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.PushNotifications
{
    public static class PushPayloads
    {
        /// <summary>
        /// A push payload carries no private content: a generic text and the ids needed to open the notification.
        /// The app fetches the real content over the authenticated API (GET /notifications/{id}), which checks the
        /// signed-in user owns it. Lock screens, OS logs and FCM never see poll text, names or phone numbers.
        /// </summary>
        public static NotificationPayload ForNotification(Notification notification)
        {
            return new NotificationPayload
            {
                Title = "Yakku",
                Body = "You have a new notification",
                Data = new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["type"] = notification.Type.ToString().ToLowerInvariant()
                }
            };
        }
    }
}
