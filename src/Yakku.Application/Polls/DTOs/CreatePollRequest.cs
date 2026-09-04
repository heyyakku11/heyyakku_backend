namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = [];
        public DateTime? ExpiresAt { get; set; }
    }
}
