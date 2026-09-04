namespace Yakku.Application.Polls.DTOs
{
    public class PollResponse
    {
        public Guid Id { get; set; }
        public Guid? CreatorId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PollOptionResponse> Options { get; set; } = [];
    }
}
