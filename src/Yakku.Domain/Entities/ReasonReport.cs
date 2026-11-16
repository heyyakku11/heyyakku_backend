using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A reason (the "why" a voter gave) that the owner of a poll or made check reported as abuse. A new report is
    /// <see cref="ReasonReportStatus.Pending"/>: the reason stays on the vote and shows to the owner flagged as
    /// reported. When an admin approves the report the reason text is removed from the vote (this row keeps the
    /// snapshot for moderation); when an admin rejects it the reason carries on as before. The content and vote are
    /// not foreign keys, so the log outlives them.
    /// </summary>
    public class ReasonReport
    {
        public const int MaxReporterReasonLength = 300;

        public Guid Id { get; private set; }
        public Guid ReporterId { get; private set; }
        public ShareContentType ContentType { get; private set; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; private set; }
        public Guid VoteId { get; private set; }
        public Guid? OptionId { get; private set; }

        /// <summary>The reported reason, as it was when reported.</summary>
        public string ReasonText { get; private set; } = string.Empty;

        /// <summary>
        /// Why the reporter reported it, in their own words as the client sent it (the backend defines no list), up
        /// to <see cref="MaxReporterReasonLength"/> characters.
        /// </summary>
        public string ReporterReason { get; private set; } = string.Empty;
        public ReasonReportStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }

        /// <summary>When an admin approved or rejected the report; null while pending.</summary>
        public DateTime? ReviewedAt { get; private set; }

        private ReasonReport()
        {
        }

        public static ReasonReport Create(
            Guid reporterId,
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            Guid? optionId,
            string reasonText,
            string reporterReason)
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
                ReporterReason = reporterReason,
                Status = ReasonReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
        }

        /// <summary>Upholds the report. Only a pending report can be decided.</summary>
        public bool Approve(DateTime now)
        {
            return Decide(ReasonReportStatus.Approved, now);
        }

        /// <summary>Dismisses the report. Only a pending report can be decided.</summary>
        public bool Reject(DateTime now)
        {
            return Decide(ReasonReportStatus.Rejected, now);
        }

        private bool Decide(ReasonReportStatus status, DateTime now)
        {
            if (Status != ReasonReportStatus.Pending)
            {
                return false;
            }

            Status = status;
            ReviewedAt = now;
            return true;
        }
    }
}
