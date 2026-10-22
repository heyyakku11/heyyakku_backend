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

        /// <summary>When the user asked to delete the account. Null while the account is not pending deletion.</summary>
        public DateTime? DeletionRequestedAt { get; private set; }

        /// <summary>When the account is permanently purged unless the user logs in again before then.</summary>
        public DateTime? DeletionScheduledFor { get; private set; }

        public bool IsPendingDeletion => DeletionScheduledFor.HasValue;

        /// <summary>The user's current plan. Every user has one; new users start on the free plan.</summary>
        public Guid PlanId { get; private set; } = Plan.FreeId;

        public Plan Plan { get; private set; } = null!;
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
            PlanId = Plan.FreeId;
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

        /// <summary>Schedules the account for permanent deletion after <paramref name="gracePeriod"/>.</summary>
        public void RequestDeletion(DateTime utcNow, TimeSpan gracePeriod)
        {
            DeletionRequestedAt = utcNow;
            DeletionScheduledFor = utcNow + gracePeriod;
            UpdatedAt = utcNow;
        }

        /// <summary>Discards a pending deletion. Returns false when none was pending.</summary>
        public bool CancelDeletion()
        {
            if (!IsPendingDeletion)
            {
                return false;
            }

            DeletionRequestedAt = null;
            DeletionScheduledFor = null;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }
    }
}
