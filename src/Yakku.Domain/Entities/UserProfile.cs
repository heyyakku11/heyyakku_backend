namespace Yakku.Domain.Entities
{
    public class UserProfile
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string AnonymousName { get; private set; } = string.Empty;

        /// <summary>
        /// "Let people I answer ask me back": when false, nobody can start an Ask Back conversation with this user
        /// about a vote they cast. On by default; the user can switch it independently of push notifications.
        /// </summary>
        public bool AllowAskBack { get; private set; } = true;
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
