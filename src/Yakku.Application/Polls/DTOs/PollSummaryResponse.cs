namespace Yakku.Application.Polls.DTOs
{
    public class PollSummaryResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
        public string OptionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
