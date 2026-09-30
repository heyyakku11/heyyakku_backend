namespace Yakku.Application.YakkuDirectory.DTOs
{
    public class YakkuPollResponse
    {
        public Guid Id { get; set; }
        public Guid CreatorId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string OptionType { get; set; } = string.Empty;
        public int TotalVoteCount { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
