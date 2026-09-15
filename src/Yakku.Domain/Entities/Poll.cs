using System.Security.Cryptography;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Poll
    {
        public Guid Id { get; private set; }
        public Guid CreatorId { get; private set; }
        public Guid? CategoryId { get; private set; }
        public string ShareToken { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public OptionType OptionType { get; private set; }
        public PollStatus Status { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime? ClosedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public int TotalVoteCount { get; private set; }

        public User Creator { get; private set; } = null!;
        public Category? Category { get; private set; }
        public ICollection<PollOption> Options { get; private set; } = new List<PollOption>();
        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();

        private Poll()
        {
        }

        public Poll(
            Guid creatorId,
            string question,
            OptionType optionType,
            Guid? categoryId = null,
            DateTime? expiresAt = null)
        {
            Id = Guid.NewGuid();
            CreatorId = creatorId;
            CategoryId = categoryId;
            ShareToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            Question = question;
            OptionType = optionType;
            Status = PollStatus.Active;
            ExpiresAt = ToUtc(expiresAt);
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

        public bool TrySoftDelete()
        {
            if (Status == PollStatus.Deleted)
            {
                return false;
            }

            Status = PollStatus.Deleted;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }
    }
}
