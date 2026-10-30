namespace Yakku.Domain.Entities
{
    /// <summary>A participant's report of the other one, for moderation. Kept when either account is deleted.</summary>
    public class AskBackReport
    {
        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }
        public Guid? ReporterUserId { get; private set; }
        public Guid? ReportedUserId { get; private set; }
        public string Reason { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        private AskBackReport()
        {
        }

        public static AskBackReport Create(
            Guid conversationId,
            Guid reporterUserId,
            Guid? reportedUserId,
            string reason,
            DateTime utcNow)
        {
            return new AskBackReport
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                ReporterUserId = reporterUserId,
                ReportedUserId = reportedUserId,
                Reason = reason,
                CreatedAt = utcNow
            };
        }
    }
}
