namespace Yakku.Domain.Entities
{
    public class UserProfile
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string DisplayName { get; private set; } = string.Empty;
        public Guid? AvatarImageId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Image? AvatarImage { get; private set; }

        private UserProfile()
        {
        }

        public UserProfile(Guid userId, string displayName)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            DisplayName = displayName;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
