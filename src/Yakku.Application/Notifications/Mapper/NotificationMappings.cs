using System.Text.Json;
using Yakku.Application.Notifications.DTOs;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Notifications.Mapper
{
    public static class NotificationMappings
    {
        public static NotificationResponse ToResponse(this Notification notification)
        {
            var data = ParseData(notification.Data);
            return new NotificationResponse
            {
                Id = notification.Id,
                Type = notification.Type.ToString().ToLowerInvariant(),
                EventType = notification.EventType.ToString().ToLowerInvariant(),
                Title = notification.Title,
                Body = notification.Body,
                Data = notification.Data,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt,
                ExpiresAt = notification.ExpiresAt,
                Actor = BuildActor(notification.EventType, data),
                Target = BuildTarget(notification, data)
            };
        }

        private static NotificationActorResponse? BuildActor(
            NotificationEventType eventType,
            NotificationDataPayload data)
        {
            if (eventType is not (NotificationEventType.AskBackStarted or NotificationEventType.AskBackReply))
            {
                return null;
            }

            return new NotificationActorResponse
            {
                UserId = data.ActorUserId,
                AnonymousName = data.ActorAnonymousName,
                AvatarTone = data.AvatarTone
            };
        }

        private static NotificationTargetResponse BuildTarget(
            Notification notification,
            NotificationDataPayload data)
        {
            return notification.EventType switch
            {
                NotificationEventType.AskBackStarted or NotificationEventType.AskBackReply => new NotificationTargetResponse
                {
                    Screen = "askBackConversation",
                    ConversationId = data.ConversationId,
                    ContentType = data.ContentType,
                    ContentId = data.ContentId,
                    DeepLink = data.DeepLink
                        ?? (data.ConversationId is { } id ? $"yakku://ask-back/{id}" : null)
                },
                NotificationEventType.PersonalityReady => new NotificationTargetResponse
                {
                    // Celebration "your card / results ready" — only opened from this ready push.
                    Screen = "personalityReady",
                    ContentType = data.ContentType ?? "personality",
                    ContentId = data.ContentId,
                    DeepLink = data.DeepLink
                        ?? (data.ContentId is { } id
                            ? $"yakku://results/personality/{id}"
                            : null)
                },
                NotificationEventType.NoVotesYet => new NotificationTargetResponse
                {
                    Screen = "cardShare",
                    ContentType = data.ContentType,
                    ContentId = data.ContentId,
                    DeepLink = data.DeepLink
                        ?? BuildResultsDeepLink(data.ContentType, data.ContentId)
                },
                NotificationEventType.PollAnswered
                    when string.Equals(
                        notification.Title,
                        PushNotifications.CardActivityCopy.ResultReadyTitle,
                        StringComparison.Ordinal) => new NotificationTargetResponse
                {
                    Screen = "cardResultsReady",
                    ContentType = data.ContentType,
                    ContentId = data.ContentId,
                    DeepLink = data.DeepLink
                        ?? BuildResultsDeepLink(data.ContentType, data.ContentId)
                },
                _ => new NotificationTargetResponse
                {
                    Screen = "cardResults",
                    ContentType = data.ContentType,
                    ContentId = data.ContentId,
                    DeepLink = data.DeepLink
                        ?? BuildResultsDeepLink(data.ContentType, data.ContentId)
                }
            };
        }

        private static string? BuildResultsDeepLink(string? contentType, Guid? contentId)
        {
            if (string.IsNullOrWhiteSpace(contentType) || contentId is null)
            {
                return null;
            }

            return $"yakku://results/{contentType}/{contentId}";
        }

        private static NotificationDataPayload ParseData(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new NotificationDataPayload();
            }

            try
            {
                return JsonSerializer.Deserialize<NotificationDataPayload>(
                           json,
                           new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                       ?? new NotificationDataPayload();
            }
            catch (JsonException)
            {
                return new NotificationDataPayload();
            }
        }

        private sealed class NotificationDataPayload
        {
            public Guid? ConversationId { get; set; }
            public Guid? MessageId { get; set; }
            public Guid? ActorUserId { get; set; }
            public string? ActorAnonymousName { get; set; }
            public string? AvatarTone { get; set; }
            public string? ContentType { get; set; }
            public Guid? ContentId { get; set; }
            public string? DeepLink { get; set; }
            public string? ShareCode { get; set; }
            public string? Kind { get; set; }
        }
    }
}
