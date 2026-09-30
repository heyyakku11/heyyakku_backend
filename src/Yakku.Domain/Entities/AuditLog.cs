using System.Net;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; private set; }
        public Guid AdminId { get; private set; }
        public AuditAction Action { get; private set; }
        public string ResourceType { get; private set; } = string.Empty;
        public Guid ResourceId { get; private set; }
        public string? ResourceName { get; private set; }
        public string? OldValue { get; private set; }
        public string? NewValue { get; private set; }
        public IPAddress? IpAddress { get; private set; }
        public string? UserAgent { get; private set; }
        public string? Metadata { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Admin Admin { get; private set; } = null!;

        private AuditLog()
        {
        }

        public AuditLog(
            Guid adminId,
            AuditAction action,
            string resourceType,
            Guid resourceId,
            string? resourceName = null,
            string? oldValue = null,
            string? newValue = null,
            IPAddress? ipAddress = null,
            string? userAgent = null,
            string? metadata = null)
        {
            Id = Guid.NewGuid();
            AdminId = adminId;
            Action = action;
            ResourceType = resourceType;
            ResourceId = resourceId;
            ResourceName = resourceName;
            OldValue = oldValue;
            NewValue = newValue;
            IpAddress = ipAddress;
            UserAgent = userAgent;
            Metadata = metadata;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
