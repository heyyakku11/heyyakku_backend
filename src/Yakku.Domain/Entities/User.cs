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

        /// <summary>Pending deletion ticket, if any. Null while the account is not pending deletion.</summary>
        public UserDeletionRequest? DeletionRequest { get; private set; }

        public bool IsPendingDeletion => DeletionRequest is not null;

        /// <summary>The user's current plan. Every user has one; new users start on the free plan.</summary>
        public Guid PlanId { get; private set; } = Plan.FreeId;

        public Plan Plan { get; private set; } = null!;

        /// <summary>When the current paid plan was activated. Null on the free plan.</summary>
        public DateTime? PlanActivatedAt { get; private set; }

        /// <summary>When the current paid plan ends. Null on the free plan. After it, the user counts as free.</summary>
        public DateTime? PlanExpiresAt { get; private set; }

        /// <summary>How the user appears in the app. Unique, compared without regard to case.</summary>
        public string AnonymousName { get; private set; } = string.Empty;

        /// <summary>The user's permissions (ask back, push, SMS, WhatsApp), created together with the account.</summary>
        public UserPermission Permissions { get; private set; } = null!;

        /// <summary>The user's personality card, created together with the account.</summary>
        public PersonalityCard PersonalityCard { get; private set; } = null!;
        public ICollection<UserSession> Sessions { get; private set; } = new List<UserSession>();
        public ICollection<Poll> Polls { get; private set; } = new List<Poll>();
        public ICollection<SavedCard> SavedCards { get; private set; } = new List<SavedCard>();
        public ICollection<MediaUploadSession> UploadSessions { get; private set; } = new List<MediaUploadSession>();
        public ICollection<Device> Devices { get; private set; } = new List<Device>();
        public ICollection<Notification> Notifications { get; private set; } = new List<Notification>();

        private User()
        {
        }

        public User(string phoneNumber, string anonymousName)
        {
            Id = Guid.NewGuid();
            PhoneNumber = phoneNumber;
            Status = UserStatus.Active;
            PlanId = Plan.FreeId;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            LoginAt = DateTime.UtcNow;
            AnonymousName = anonymousName;
            Permissions = new UserPermission(Id);
            PersonalityCard = new PersonalityCard(Id);
        }

        /// <summary>
        /// Puts the user on <paramref name="plan"/> from <paramref name="utcNow"/>. A plan with a duration ends that many
        /// days later; activating again restarts the period (and the tickets that count from the activation time).
        /// </summary>
        public void ActivatePlan(Plan plan, DateTime utcNow)
        {
            PlanId = plan.Id;
            if (plan.DurationDays is { } days)
            {
                PlanActivatedAt = utcNow;
                PlanExpiresAt = utcNow.AddDays(days);
            }
            else
            {
                PlanActivatedAt = null;
                PlanExpiresAt = null;
            }

            UpdatedAt = utcNow;
        }

        /// <summary>
        /// False once a paid plan's end has passed. The free plan never expires, and neither does a paid plan that
        /// has no duration. Callers treat an inactive paid plan as the free plan.
        /// </summary>
        public bool IsPlanActive(DateTime utcNow)
        {
            return PlanId == Plan.FreeId || PlanExpiresAt is null || PlanExpiresAt > utcNow;
        }

        public void RecordLogin()
        {
            LoginAt = DateTime.UtcNow;
            UpdatedAt = LoginAt.Value;
        }

        /// <summary>Schedules the account for permanent deletion after <paramref name="gracePeriod"/> and suspends the user.</summary>
        public UserDeletionRequest RequestDeletion(DateTime utcNow, TimeSpan gracePeriod)
        {
            var request = UserDeletionRequest.Create(Id, utcNow, gracePeriod);
            DeletionRequest = request;
            Status = UserStatus.Suspended;
            UpdatedAt = utcNow;
            return request;
        }

        /// <summary>Discards a pending deletion and restores Active. Returns false when none was pending.</summary>
        public bool CancelDeletion()
        {
            if (!IsPendingDeletion)
            {
                return false;
            }

            DeletionRequest = null;
            Status = UserStatus.Active;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }
    }
}
