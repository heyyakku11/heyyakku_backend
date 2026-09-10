using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Image
    {
        public Guid Id { get; private set; }
        public ImageProvider Provider { get; private set; }
        public string PublicId { get; private set; } = string.Empty;
        public string Url { get; private set; } = string.Empty;
        public string SecureUrl { get; private set; } = string.Empty;
        public string? ResourceType { get; private set; }
        public string? Format { get; private set; }
        public int? Width { get; private set; }
        public int? Height { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public ICollection<UserProfile> UserProfiles { get; private set; } = new List<UserProfile>();
        public ICollection<PollOptions> PollOptions { get; private set; } = new List<PollOptions>();
        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();
        public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();

        private Image()
        {
        }

        public Image(
            ImageProvider provider,
            string publicId,
            string url,
            string secureUrl,
            string? resourceType = null,
            string? format = null,
            int? width = null,
            int? height = null)
        {
            Id = Guid.NewGuid();
            Provider = provider;
            PublicId = publicId;
            Url = url;
            SecureUrl = secureUrl;
            ResourceType = resourceType;
            Format = format;
            Width = width;
            Height = height;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
