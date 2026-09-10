namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollRequest
    {
        public string Question { get; set; } = string.Empty;
        public Guid? CategoryId { get; set; }
        public string OptionType { get; set; } = string.Empty;
        public List<CreatePollOptionRequest> Options { get; set; } = [];
        public DateTime? ExpiresAt { get; set; }
    }
}
