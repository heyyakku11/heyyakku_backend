namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollOptionRequest
    {
        public string? Text { get; set; }
        public int? DisplayOrder { get; set; }
    }
}
