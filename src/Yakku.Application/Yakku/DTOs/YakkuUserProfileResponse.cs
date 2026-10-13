namespace Yakku.Application.YakkuDirectory.DTOs
{
    public class YakkuUserProfileResponse
    {
        public Guid Id { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string AnonymousName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? LoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
