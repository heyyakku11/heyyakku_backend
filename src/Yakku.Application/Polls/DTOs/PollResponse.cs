namespace Yakku.Application.Polls.DTOs
{
    public class PollResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string ShareToken { get; set; } = string.Empty;
        public string OptionType { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public int TotalVoteCount { get; set; }
        public Guid? SelectedOptionId { get; set; }
        public List<PollOptionResponse> Options { get; set; } = [];
    }
}
