namespace Yakku.Application.Polls.DTOs
{
    public class PollResponse
    {
        public Guid Id { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public string MediaReadiness { get; set; } = "none";
        public PollMediaResponse? Media { get; set; }
        public int TotalVoteCount { get; set; }
        public int MinVotes { get; set; }
        public int VoteProgress { get; set; }
        public bool ResultsUnlocked { get; set; }
        public int CustomAnswerCount { get; set; }
        public int TotalWhyCount { get; set; }
        public List<PollOptionResponse> Options { get; set; } = [];
    }
}
