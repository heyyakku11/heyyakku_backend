using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; } = string.Empty;
        public UserStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }

        public UserProfile Profile { get; private set; } = null!;
        public NotificationPreference? NotificationPreference { get; private set; }
        public ICollection<UserSession> Sessions { get; private set; } = new List<UserSession>();
        public ICollection<Polls> Polls { get; private set; } = new List<Polls>();
        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();
        public ICollection<Device> Devices { get; private set; } = new List<Device>();
        public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();
        public ICollection<SystemLog> SystemLogs { get; private set; } = new List<SystemLog>();

        private User()
        {
        }

        public User(string email, string displayName)
        {
            Id = Guid.NewGuid();
            Email = email;
            Status = UserStatus.Active;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            LastLoginAt = DateTime.UtcNow;
            Profile = new UserProfile(Id, displayName);
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            UpdatedAt = LastLoginAt.Value;
        }
    }
}
