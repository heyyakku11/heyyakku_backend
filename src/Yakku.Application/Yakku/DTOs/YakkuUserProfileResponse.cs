namespace Yakku.Application.YakkuDirectory.DTOs
{
    public class YakkuUserProfileResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
