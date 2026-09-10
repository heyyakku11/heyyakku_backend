namespace Yakku.Application.Polls.DTOs
{
    public class PollResponse
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
        public string ShareToken { get; set; } = string.Empty;
        public string OptionType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PollOptionResponse> Options { get; set; } = [];
    }
}
