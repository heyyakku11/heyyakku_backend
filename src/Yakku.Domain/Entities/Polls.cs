using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Polls
    {
        public Guid Id { get; private set; }
        public Guid? CreatorId { get; private set; }
        public string Question { get; private set; } = string.Empty;
        public PollStatus Status { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public ICollection<PollOptions> Options { get; private set; } = new List<PollOptions>();

        private Polls()
        {
        }

        public Polls(Guid? creatorId, string question, DateTime? expiresAt = null)
        {
            Id = Guid.NewGuid();
            CreatorId = creatorId;
            Question = question;
            Status = PollStatus.Active;
            ExpiresAt = ToUtc(expiresAt);
            CreatedAt = DateTime.UtcNow;
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

        public void AddOption(string text, int position)
        {
            Options.Add(new PollOptions(Id, text, position));
        }
    }
}
