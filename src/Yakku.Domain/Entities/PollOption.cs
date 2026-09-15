namespace Yakku.Domain.Entities
{
    public class PollOption
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public string? Text { get; private set; }
        public Guid? ImageId { get; private set; }
        public int SortOrder { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public int VoteCount { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public Image? Image { get; private set; }
        public ICollection<Vote> Votes { get; private set; } = new List<Vote>();

        private PollOption()
        {
        }

        public static PollOption CreateText(Guid pollId, string text, int sortOrder)
        {
            return new PollOption
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                Text = text,
                ImageId = null,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VoteCount = 0
            };
        }

        public static PollOption CreateImage(Guid pollId, Guid imageId, int sortOrder)
        {
            return new PollOption
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                Text = null,
                ImageId = imageId,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                VoteCount = 0
            };
        }
    }
}
