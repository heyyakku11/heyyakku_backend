namespace Yakku.Application.Polls.DTOs
{
    public class PollOptionResponse
    {
        public Guid? Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsSystemOption { get; set; }
    }
}
