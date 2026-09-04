namespace Yakku.Application.Votes.DTOs
{
    public class VoteResponse
    {
        public Guid Id { get; set; }
        public Guid PollId { get; set; }
        public Guid? PollOptionId { get; set; }
        public string? CustomOptionText { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
