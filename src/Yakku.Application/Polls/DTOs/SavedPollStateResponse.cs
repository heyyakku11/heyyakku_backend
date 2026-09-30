namespace Yakku.Application.Polls.DTOs
{
    public class SavedPollStateResponse
    {
        public Guid PollId { get; set; }
        public bool Saved { get; set; }
    }
}
