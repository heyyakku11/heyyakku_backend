using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; } = string.Empty;
        public UserStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        public UserProfile Profile { get; private set; } = null!;
        public ICollection<UserSession> Sessions { get; private set; } = new List<UserSession>();

        private User()
        {
        }

        public User(string email, string displayName)
        {
            Id = Guid.NewGuid();
            Email = email;
            Status = UserStatus.Active;
            CreatedAt = DateTime.UtcNow;
            LastLoginAt = DateTime.UtcNow;
            Profile = new UserProfile(Id, displayName);
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
        }
    }
}
