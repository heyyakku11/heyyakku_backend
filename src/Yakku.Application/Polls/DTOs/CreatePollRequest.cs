namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollRequest
    {
        public string Question { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string? Visibility { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public List<CreatePollOptionRequest> Options { get; set; } = [];
        public Guid? MediaUploadSessionId { get; set; }
    }
}
