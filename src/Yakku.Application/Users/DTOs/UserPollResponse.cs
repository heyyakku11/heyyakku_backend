namespace Yakku.Application.Users.DTOs
{
    public class UserPollResponse
    {
        /// <summary>"poll" or "check". "My asks" lists both, newest first.</summary>
        public string Kind { get; set; } = "poll";

        /// <summary>The card id: the poll id, or the made-check id when <see cref="Kind"/> is "check".</summary>
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

        /// <summary>Only for checks: the check's title, background colour and sticker.</summary>
        public UserCheckStyleResponse? Check { get; set; }
    }

    public class UserCheckStyleResponse
    {
        public string Title { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
    }

    public class UserPollOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int VoteCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
