using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class User
    {
        public Guid Id { get; private set; }
        public string PhoneNumber { get; private set; } = string.Empty;
        public UserStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? LoginAt { get; private set; }

        public UserProfile Profile { get; private set; } = null!;
        public NotificationPreference? NotificationPreference { get; private set; }
        public ICollection<UserSession> Sessions { get; private set; } = new List<UserSession>();
        public ICollection<Poll> Polls { get; private set; } = new List<Poll>();
        public ICollection<SavedPoll> SavedPolls { get; private set; } = new List<SavedPoll>();
        public ICollection<PollVote> Votes { get; private set; } = new List<PollVote>();
        public ICollection<MediaUploadSession> UploadSessions { get; private set; } = new List<MediaUploadSession>();
        public ICollection<Device> Devices { get; private set; } = new List<Device>();
        public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();

        private User()
        {
        }

        public User(string phoneNumber, string name, string anonymousName)
        {
            Id = Guid.NewGuid();
            PhoneNumber = phoneNumber;
            Status = UserStatus.Active;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            LoginAt = DateTime.UtcNow;
            Profile = new UserProfile(Id, name, anonymousName);
        }

        public void RecordLogin()
        {
            LoginAt = DateTime.UtcNow;
            UpdatedAt = LoginAt.Value;
        }
    }
}
