namespace Yakku.Application.AdminReasonReports.DTOs
{
    public class AdminReasonReportResponse
    {
        public Guid Id { get; set; }

        /// <summary>poll or check.</summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; set; }

        /// <summary>The vote the reason belongs to. Also the reason's id in the owner's app.</summary>
        public Guid VoteId { get; set; }
        public Guid? OptionId { get; set; }

        /// <summary>The reported reason, as it was when reported.</summary>
        public string ReasonText { get; set; } = string.Empty;

        /// <summary>Why the owner reported it, as they sent it.</summary>
        public string ReporterReason { get; set; } = string.Empty;

        /// <summary>pending, approved or rejected.</summary>
        public string Status { get; set; } = string.Empty;

        public string ReporterAnonymousName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    /// <summary>One page of reports from the repository, with the total across all pages.</summary>
    public class AdminReasonReportPage
    {
        public List<AdminReasonReportResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
