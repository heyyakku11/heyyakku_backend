namespace Yakku.Application.Users.DTOs
{
    public class UserPollDetailResponse
    {
        public Guid PollId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public int TotalVoteCount { get; set; }
        public int MinVotes { get; set; }
        public int VoteProgress { get; set; }
        public bool ResultsUnlocked { get; set; }
        public int CustomAnswerCount { get; set; }
        public int TotalWhyCount { get; set; }
        public List<UserPollDetailOptionResponse> PollOptions { get; set; } = [];

        /// <summary>Empty until results are unlocked.</summary>
        public YouVsCrowdResponse YouVsCrowd { get; set; } = new();
    }

    public class UserPollDetailOptionResponse
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public int SortOrder { get; set; }
        public int VoteCount { get; set; }
        public decimal Percentage { get; set; }
    }

    public class YouVsCrowdResponse
    {
        public Guid? YourOptionId { get; set; }
        public string? YourOptionText { get; set; }
        public decimal? YourOptionPercentage { get; set; }
        public Guid? CrowdLeadingOptionId { get; set; }
        public string? CrowdLeadingOptionText { get; set; }
        public decimal? CrowdLeadingPercentage { get; set; }
        public bool? AgreesWithCrowd { get; set; }
    }
}
