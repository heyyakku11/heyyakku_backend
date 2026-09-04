namespace Yakku.Application.Polls.DTOs
{
    public class CreatePollResponse
    {
        public Guid Id { get; set; }
        public Guid? CreatorId { get; set; }
        public string Question { get; set; } = string.Empty;
    }
}
