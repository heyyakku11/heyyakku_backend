namespace Yakku.Application.Polls.DTOs
{
    public class PollMediaResponse
    {
        public string MediaType { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public long? DurationMs { get; set; }
        public string? Url { get; set; }
        public DateTime? UrlExpiresAt { get; set; }
    }

    public class PollResultsResponse
    {
        public Guid PollId { get; set; }
        public int TotalVoteCount { get; set; }
        public int MinVotes { get; set; }
        public int VoteProgress { get; set; }
        public bool ResultsUnlocked { get; set; }
        public int CustomAnswerCount { get; set; }
        public int TotalWhyCount { get; set; }
        public List<PollOptionResultResponse> Options { get; set; } = [];
    }

    public class PollOptionResultResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public int VoteCount { get; set; }
    }
}
