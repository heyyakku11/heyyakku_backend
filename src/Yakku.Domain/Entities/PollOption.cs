namespace Yakku.Domain.Entities
{
    public class PollOption
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public string Text { get; private set; } = string.Empty;
        public int SortOrder { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public ICollection<PollVote> Votes { get; private set; } = new List<PollVote>();

        private PollOption()
        {
        }

        public static PollOption CreateText(Guid pollId, string text, int sortOrder)
        {
            var now = DateTime.UtcNow;
            return new PollOption
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                Text = text,
                SortOrder = sortOrder,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
