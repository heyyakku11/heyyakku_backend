namespace Yakku.Application.YakkuDirectory.DTOs
{
    public class YakkuGuestProfileResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
