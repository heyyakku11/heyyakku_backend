namespace Yakku.Application.Polls.DTOs
{
    public class SharedPollResponse
    {
        public string Question { get; set; } = string.Empty;
        public string OptionType { get; set; } = string.Empty;
        public List<PollOptionResponse> Options { get; set; } = [];
        public SharedPollCategoryResponse? Category { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsAcceptingVotes { get; set; }
    }

    public class SharedPollCategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
