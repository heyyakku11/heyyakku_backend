namespace Yakku.Domain.Entities
{
    public class UserProfile
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string AnonymousName { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private UserProfile()
        {
        }

        public UserProfile(Guid userId, string anonymousName)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            AnonymousName = anonymousName;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
