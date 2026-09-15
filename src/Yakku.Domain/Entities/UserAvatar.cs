using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class UserAvatar
    {
        public Guid Id { get; private set; }
        public string ImageUrl { get; private set; } = string.Empty;
        public string? AltText { get; private set; }
        public ImageFormat ImageFormat { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ICollection<UserProfile> UserProfiles { get; private set; } = new List<UserProfile>();

        private UserAvatar()
        {
        }

        public UserAvatar(string imageUrl, ImageFormat imageFormat, string? altText = null)
        {
            Id = Guid.NewGuid();
            ImageUrl = imageUrl;
            ImageFormat = imageFormat;
            AltText = altText;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }
    }
}
