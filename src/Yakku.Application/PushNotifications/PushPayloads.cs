using System.Text.Json;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.PushNotifications
{
    public static class PushPayloads
    {
        /// <summary>
        /// A push payload carries no private content: a generic text and the ids needed to open the notification
        /// and the card it is about (<c>contentType</c> poll|check and <c>contentId</c>, which are opaque ids; the
        /// card itself is only readable by its owner over the authenticated API). Tapping the push opens that card's
        /// results screen.
        /// The app fetches the real content over the authenticated API (GET /notifications/{id}), which checks the
        /// signed-in user owns it. Lock screens, OS logs and FCM never see poll text, names or phone numbers.
        /// </summary>
        public static NotificationPayload ForNotification(Notification notification)
        {
            var data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type.ToString().ToLowerInvariant(),
                ["eventType"] = notification.EventType.ToString()
            };

            if (TryReadTarget(notification.Data, out var contentType, out var contentId))
            {
                data["contentType"] = contentType;
                data["contentId"] = contentId;
                // The in-app route the push opens: yakku://results/{poll|check}/{id}.
                data["deepLink"] = $"yakku://results/{contentType}/{contentId}";
            }

            return new NotificationPayload
            {
                Title = "Yakku",
                Body = "You have a new notification",
                Data = data
            };
        }

        /// <summary>
        /// The summary push for a digest window: how many new answers the card got, and where to open it. Like
        /// every push it carries no voter and no answer.
        /// </summary>
        public static NotificationPayload ForDigest(string contentType, Guid contentId, int count)
        {
            return new NotificationPayload
            {
                Title = "Yakku",
                Body = count == 1 ? "1 new answer" : $"{count} new answers",
                Data = new Dictionary<string, string>
                {
                    ["type"] = "normal",
                    ["eventType"] = "PollAnswered",
                    ["digest"] = "true",
                    ["count"] = count.ToString(),
                    ["contentType"] = contentType,
                    ["contentId"] = contentId.ToString(),
                    ["deepLink"] = $"yakku://results/{contentType}/{contentId}"
                }
            };
        }

        /// <summary>
        /// The push for a new Ask Back message. Like every push it holds no message text, name or phone number:
        /// a generic line and the conversation id, which only its two participants can open.
        /// </summary>
        public static NotificationPayload ForAskBackMessage(Guid conversationId)
        {
            return new NotificationPayload
            {
                Title = "Yakku",
                Body = "You have a new message",
                Data = new Dictionary<string, string>
                {
                    ["type"] = "normal",
                    ["eventType"] = "AskBackMessage",
                    ["conversationId"] = conversationId.ToString(),
                    ["deepLink"] = $"yakku://ask-back/{conversationId}"
                }
            };
        }

        /// <summary>Reads <c>{ contentType, contentId }</c> out of a notification's data JSON; false when absent or malformed.</summary>
        private static bool TryReadTarget(string? json, out string contentType, out string contentId)
        {
            contentType = string.Empty;
            contentId = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("contentType", out var typeElement) ||
                    !root.TryGetProperty("contentId", out var idElement))
                {
                    return false;
                }

                var type = typeElement.GetString()?.ToLowerInvariant();
                var id = idElement.GetString();
                if ((type != "poll" && type != "check") || !Guid.TryParse(id, out var parsed))
                {
                    return false;
                }

                contentType = type;
                contentId = parsed.ToString();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
