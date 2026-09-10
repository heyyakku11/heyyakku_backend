namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollOptionRequest
    {
        public string? Text { get; set; }
        public Guid? ImageId { get; set; }
    }
}
