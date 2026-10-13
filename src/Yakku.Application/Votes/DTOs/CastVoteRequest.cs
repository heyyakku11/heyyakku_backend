namespace Yakku.Application.Votes.DTOs
{
    public class CastVoteRequest
    {
        public Guid PollId { get; set; }
        public Guid? PollOptionId { get; set; }
        public string? CustomAnswer { get; set; }
        public string? Reason { get; set; }
    }
}
