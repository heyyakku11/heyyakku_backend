namespace Yakku.Application.Votes.DTOs
{
    public class CastVoteRequest
    {
        public Guid? OptionId { get; set; }
        public string? CustomOption { get; set; }
        public string? Reason { get; set; }
    }
}
