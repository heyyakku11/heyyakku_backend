namespace Yakku.Application.AdminPolls.DTOs
{
    public class AdminPollResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;

        /// <summary>question, image, video or audio.</summary>
        public string PollType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        /// <summary>draft, active, closed, or expired (an active poll past its expiry).</summary>
        public string Status { get; set; } = string.Empty;

        public string CreatorAnonymousName { get; set; } = string.Empty;
        public int VoteCount { get; set; }

        /// <summary>Votes that came with a reason.</summary>
        public int ReasonCount { get; set; }
    }

    /// <summary>One page of polls from the repository, with the total across all pages.</summary>
    public class AdminPollPage
    {
        public List<AdminPollResponse> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    /// <summary>The status filter of the admin poll list.</summary>
    public enum AdminPollStatusFilter
    {
        Draft,
        Active,
        Closed,
        Expired
    }
}
