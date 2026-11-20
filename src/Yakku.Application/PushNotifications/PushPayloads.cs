using System.Text.Json;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications
{
    public static class PushPayloads
    {
        /// <summary>
        /// A push payload carries no private content: a generic text and the ids needed to open the notification
        /// and the card it is about (<c>contentType</c> poll|check|personality and <c>contentId</c>, which are opaque ids; the
        /// card itself is only readable by its owner over the authenticated API). Tapping the push opens that card's
        /// results (or share) screen via <c>screen</c> + <c>deepLink</c>.
        /// The app fetches the real content over the authenticated API (GET /notifications/{id}), which checks the
        /// signed-in user owns it. Lock screens, OS logs and FCM never see poll text, names or phone numbers.
        /// </summary>
        public static NotificationPayload ForNotification(Notification notification, Guid? deliveryId = null)
        {
            var data = new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["type"] = notification.Type.ToString().ToLowerInvariant(),
                ["eventType"] = notification.EventType.ToString()
            };

            // Stable across retries of the same push, so the app can drop a duplicate FCM delivery.
            if (deliveryId is { } id)
            {
                data["deliveryId"] = id.ToString();
            }

            if (TryReadConversationId(notification.Data, out var conversationId))
            {
                data["screen"] = "askBackConversation";
                data["conversationId"] = conversationId;
                data["deepLink"] = $"yakku://ask-back/{conversationId}";
            }
            else if (TryReadTarget(notification.Data, out var contentType, out var contentId))
            {
                data["screen"] = ScreenFor(notification.EventType);
                data["contentType"] = contentType;
                data["contentId"] = contentId;
                // The in-app route the push opens: yakku://results/{poll|check|personality}/{id}.
                data["deepLink"] = $"yakku://results/{contentType}/{contentId}";
            }

            return new NotificationPayload
            {
                Title = "Yakku",
                Body = notification.EventType is NotificationEventType.AskBackStarted
                    or NotificationEventType.AskBackReply
                    ? "You have a new message"
                    : "You have a new notification",
                Data = data
            };
        }

        public const string BatchTitle = "Your card is getting attention!";

        /// <summary>
        /// The aggregated push for a batch of votes on one card: how many people voted, and where to open it. Like
        /// every push it carries no voter and no answer. <c>deliveryId</c> is the batch id and stays the same when
        /// the push is retried, so the app can drop a duplicate; <c>cardId</c> is the card (poll, check, or personality) id.
        /// The keys the earlier digest push carried (<c>digest</c>, <c>count</c>, <c>contentType</c>, <c>contentId</c>,
        /// <c>deepLink</c>) are unchanged, so opening it still lands on the card's results.
        /// </summary>
        public static NotificationPayload ForBatch(string contentType, Guid contentId, int count, Guid batchId)
        {
            var people = count == 1 ? "1 person has" : $"{count} people have";
            return new NotificationPayload
            {
                Title = BatchTitle,
                Body = $"{people} voted on your card. Tap to see the results.",
                Data = new Dictionary<string, string>
                {
                    ["type"] = "normal",
                    ["eventType"] = "PollAnswered",
                    ["screen"] = "cardResults",
                    ["digest"] = "true",
                    ["count"] = count.ToString(),
                    ["contentType"] = contentType,
                    ["contentId"] = contentId.ToString(),
                    ["cardId"] = contentId.ToString(),
                    ["batchId"] = batchId.ToString(),
                    ["deliveryId"] = batchId.ToString(),
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
                    ["screen"] = "askBackConversation",
                    ["conversationId"] = conversationId.ToString(),
                    ["deepLink"] = $"yakku://ask-back/{conversationId}"
                }
            };
        }

        /// <summary>
        /// Same screen names the inbox API returns in <c>target.screen</c>
        /// (askBackConversation | cardResults | personalityResults | cardShare).
        /// </summary>
        public static string ScreenFor(NotificationEventType eventType)
        {
            return eventType switch
            {
                NotificationEventType.AskBackStarted or NotificationEventType.AskBackReply => "askBackConversation",
                NotificationEventType.PersonalityReady => "personalityResults",
                NotificationEventType.NoVotesYet => "cardShare",
                _ => "cardResults"
            };
        }

        /// <summary>Reads Ask Back <c>conversationId</c> from notification data JSON.</summary>
        private static bool TryReadConversationId(string? json, out string conversationId)
        {
            conversationId = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("conversationId", out var idElement))
                {
                    return false;
                }

                var id = idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : idElement.GetRawText().Trim('"');
                if (!Guid.TryParse(id, out var parsed))
                {
                    return false;
                }

                conversationId = parsed.ToString();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
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
                var id = idElement.ValueKind == JsonValueKind.String
                    ? idElement.GetString()
                    : idElement.GetRawText().Trim('"');
                if ((type != "poll" && type != "check" && type != "personality") || !Guid.TryParse(id, out var parsed))
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
