using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Report
    {
        public Guid Id { get; private set; }
        public Guid? ReportedByUserId { get; private set; }
        public Guid? ReportedByGuestId { get; private set; }
        public ResourceType ResourceType { get; private set; }
        public Guid ResourceId { get; private set; }
        public ReportReason Reason { get; private set; }
        public string? Description { get; private set; }
        public ReportStatus Status { get; private set; }
        public Guid? ReviewedBy { get; private set; }
        public DateTime? ReviewedAt { get; private set; }
        public string? ReviewNotes { get; private set; }
        public ReportAction? Action { get; private set; }
        public DateTime? ActionTakenAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public User? ReportedByUser { get; private set; }
        public Guest? ReportedByGuest { get; private set; }
        public Admin? ReviewedByAdmin { get; private set; }

        private Report()
        {
        }

        public Report(
            ResourceType resourceType,
            Guid resourceId,
            ReportReason reason,
            Guid? reportedByUserId = null,
            Guid? reportedByGuestId = null,
            string? description = null)
        {
            Id = Guid.NewGuid();
            ReportedByUserId = reportedByUserId;
            ReportedByGuestId = reportedByGuestId;
            ResourceType = resourceType;
            ResourceId = resourceId;
            Reason = reason;
            Description = description;
            Status = ReportStatus.Pending;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
