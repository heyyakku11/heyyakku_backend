using System.Buffers.Text;
using System.Security.Cryptography;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Poll
    {
        public Guid Id { get; private set; }
        public Guid CreatorId { get; private set; }
        public string ShareToken { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public OptionType OptionType { get; private set; }
        public PollStatus Status { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public bool AllowComments { get; private set; }
        public DateTime? ClosedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? DeletedAt { get; private set; }
        public Guid? DeletedBy { get; private set; }
        public string? DeletedReason { get; private set; }
        public int TotalVoteCount { get; private set; }

        public User Creator { get; private set; } = null!;
        public ICollection<PollCategory> PollCategories { get; private set; } = new List<PollCategory>();
        public ICollection<PollOption> Options { get; private set; } = new List<PollOption>();
        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();
        public ICollection<Comment> Comments { get; private set; } = new List<Comment>();
        public ICollection<SavedPoll> SavedPolls { get; private set; } = new List<SavedPoll>();
        public PollAnalytics? PollAnalytics { get; private set; }

        private Poll()
        {
        }

        public Poll(
            Guid creatorId,
            string question,
            OptionType optionType,
            DateTime? expiresAt = null,
            bool allowComments = false)
        {
            Id = Guid.NewGuid();
            CreatorId = creatorId;
            ShareToken = Base64Url.EncodeToString(
                SHA256.HashData(RandomNumberGenerator.GetBytes(32)));
            Question = question;
            OptionType = optionType;
            Status = PollStatus.Active;
            ExpiresAt = ToUtc(expiresAt);
            AllowComments = allowComments;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
            TotalVoteCount = 0;
        }

        private static DateTime? ToUtc(DateTime? value)
        {
            if (value is null)
            {
                return null;
            }

            return value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        public void AddCategory(Guid categoryId)
        {
            PollCategories.Add(new PollCategory(Id, categoryId));
        }

        public void AddTextOption(string text, int sortOrder)
        {
            Options.Add(PollOption.CreateText(Id, text, sortOrder));
        }

        public void AddImageOption(Guid imageId, int sortOrder)
        {
            Options.Add(PollOption.CreateImage(Id, imageId, sortOrder));
        }

        public bool TryClose()
        {
            if (Status == PollStatus.Deleted)
            {
                return false;
            }

            if (Status == PollStatus.Closed)
            {
                return false;
            }

            if (Status != PollStatus.Active)
            {
                return false;
            }

            Status = PollStatus.Closed;
            ClosedAt = DateTime.UtcNow;
            UpdatedAt = ClosedAt.Value;
            return true;
        }

        public bool TrySoftDelete(Guid? deletedBy = null, string? deletedReason = null)
        {
            if (Status == PollStatus.Deleted)
            {
                return false;
            }

            Status = PollStatus.Deleted;
            DeletedAt = DateTime.UtcNow;
            DeletedBy = deletedBy;
            DeletedReason = deletedReason;
            UpdatedAt = DeletedAt.Value;
            return true;
        }
    }
}
