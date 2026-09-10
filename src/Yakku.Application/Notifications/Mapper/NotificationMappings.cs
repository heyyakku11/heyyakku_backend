using Yakku.Application.Notifications.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Notifications.Mapper
{
    internal static class NotificationMappings
    {
        public static NotificationResponse ToResponse(this Notification notification)
        {
            return new NotificationResponse
            {
                Id = notification.Id,
                Type = notification.Type.ToString().ToLowerInvariant(),
                EventType = notification.EventType.ToString().ToLowerInvariant(),
                Title = notification.Title,
                Body = notification.Body,
                ImageId = notification.ImageId,
                Data = notification.Data,
                IsRead = notification.IsRead,
                ReadAt = notification.ReadAt,
                CreatedAt = notification.CreatedAt,
                ExpiresAt = notification.ExpiresAt
            };
        }
    }
}
