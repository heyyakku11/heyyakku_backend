namespace Yakku.Application.Users.DTOs
{
    public class UserProfileResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public DateTime? LoginAt { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
