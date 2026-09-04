using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class SystemLog
    {
        public Guid Id { get; private set; }
        public SystemLogLevel Level { get; private set; }
        public string EventType { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string? Details { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? GuestId { get; private set; }
        public string? Path { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private SystemLog()
        {
        }

        public SystemLog(
            SystemLogLevel level,
            string eventType,
            string message,
            string? details,
            Guid? userId,
            Guid? guestId,
            string? path)
        {
            Id = Guid.NewGuid();
            Level = level;
            EventType = eventType;
            Message = message;
            Details = details;
            UserId = userId;
            GuestId = guestId;
            Path = path;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
