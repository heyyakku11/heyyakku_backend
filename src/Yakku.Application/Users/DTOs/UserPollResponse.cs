namespace Yakku.Application.Users.DTOs
{
    public class UserPollResponse
    {
        public Guid PollId { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<UserPollOptionResponse> PollOptions { get; set; } = [];
    }

    public class UserPollOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
