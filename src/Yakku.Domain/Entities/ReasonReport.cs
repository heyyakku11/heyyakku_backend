using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Log of a reason (the "why" a voter gave) that the owner of a poll or made check reported as abuse. The
    /// reason text is removed from the vote so the owner no longer sees it; this row keeps a snapshot of it for
    /// moderation. The content and vote are not foreign keys, so the log outlives them.
    /// </summary>
    public class ReasonReport
    {
        public Guid Id { get; private set; }
        public Guid ReporterId { get; private set; }
        public ShareContentType ContentType { get; private set; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; private set; }
        public Guid VoteId { get; private set; }
        public Guid? OptionId { get; private set; }

        /// <summary>The reported reason, as it was when reported.</summary>
        public string ReasonText { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        private ReasonReport()
        {
        }

        public static ReasonReport Create(
            Guid reporterId,
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            Guid? optionId,
            string reasonText)
        {
            return new ReasonReport
            {
                Id = Guid.NewGuid(),
                ReporterId = reporterId,
                ContentType = contentType,
                ContentId = contentId,
                VoteId = voteId,
                OptionId = optionId,
                ReasonText = reasonText,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
