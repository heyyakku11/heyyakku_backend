using Yakku.Application.Common.Responses;

namespace Yakku.Application.Notifications.DTOs
{
    public class NotificationsPage
    {
        public List<NotificationResponse> Items { get; set; } = [];
        public PaginationMeta Meta { get; set; } = new();
    }
}
