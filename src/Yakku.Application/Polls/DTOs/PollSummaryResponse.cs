namespace Yakku.Application.Polls.DTOs
{
    public class PollSummaryResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Visibility { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int TotalVoteCount { get; set; }
        public int CustomAnswerCount { get; set; }
        public int TotalWhyCount { get; set; }
    }
}
