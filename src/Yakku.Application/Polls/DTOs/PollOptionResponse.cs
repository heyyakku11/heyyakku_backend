namespace Yakku.Application.Polls.DTOs
{
    public class PollOptionResponse
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public int SortOrder { get; set; }
        public int VoteCount { get; set; }
        public int WhyCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
