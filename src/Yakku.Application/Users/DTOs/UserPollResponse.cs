namespace Yakku.Application.Users.DTOs
{
    public class UserPollResponse
    {
        public Guid PollId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int TotalVoteCount { get; set; }
        public int MinVotes { get; set; }
        public int VoteProgress { get; set; }
        public bool ResultsUnlocked { get; set; }
        public int CustomAnswerCount { get; set; }
        public int TotalWhyCount { get; set; }
        public List<UserPollOptionResponse> PollOptions { get; set; } = [];
    }

    public class UserPollOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
