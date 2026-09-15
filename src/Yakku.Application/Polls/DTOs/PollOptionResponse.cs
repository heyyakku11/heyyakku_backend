namespace Yakku.Application.Polls.DTOs
{
    public class PollOptionResponse
    {
        public Guid Id { get; set; }
        public string? Text { get; set; }
        public Guid? ImageId { get; set; }
        public string? SecureUrl { get; set; }
        public int SortOrder { get; set; }
        public int VoteCount { get; set; }
        public decimal Percentage { get; set; }
    }
}
