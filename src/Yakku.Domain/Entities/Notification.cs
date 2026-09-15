using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public NotificationType Type { get; private set; }
        public NotificationEventType EventType { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Body { get; private set; } = string.Empty;
        public string? Data { get; private set; }
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ExpiresAt { get; private set; }

        public User User { get; private set; } = null!;

        private Notification()
        {
        }

        public Notification(
            Guid userId,
            NotificationType type,
            NotificationEventType eventType,
            string title,
            string body,
            DateTime? expiresAt = null,
            string? data = null)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            Type = type;
            EventType = eventType;
            Title = title;
            Body = body;
            Data = data;
            IsRead = false;
            CreatedAt = DateTime.UtcNow;
            ExpiresAt = expiresAt;
        }

        public void MarkAsRead()
        {
            if (IsRead)
            {
                return;
            }

            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }
    }
}
