using System.Net;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class SystemLog
    {
        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? AdminId { get; private set; }
        public Guid? GuestId { get; private set; }
        public SystemEventType EventType { get; private set; }
        public LogSeverity Severity { get; private set; }
        public string? ResourceType { get; private set; }
        public Guid? ResourceId { get; private set; }
        public string? Message { get; private set; }
        public IPAddress? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public User? User { get; private set; }
        public Admin? Admin { get; private set; }
        public Guest? Guest { get; private set; }

        private SystemLog()
        {
        }

        public SystemLog(
            LogSeverity severity,
            SystemEventType eventType,
            string? message,
            string? metadata,
            Guid? userId,
            Guid? guestId,
            IPAddress? ipAddress = null,
            string? userAgent = null,
            Guid? adminId = null,
            string? resourceType = null,
            Guid? resourceId = null)
        {
            Id = Guid.NewGuid();
            Severity = severity;
            EventType = eventType;
            Message = message;
            Metadata = metadata;
            UserId = userId;
            AdminId = adminId;
            GuestId = guestId;
            ResourceType = resourceType;
            ResourceId = resourceId;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
