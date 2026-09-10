namespace Yakku.Domain.Entities
{
    public class PollOptions
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public string? Text { get; private set; }
        public Guid? ImageId { get; private set; }
        public int SortOrder { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Polls Poll { get; private set; } = null!;
        public Image? Image { get; private set; }

        private PollOptions()
        {
        }

        public static PollOptions CreateText(Guid pollId, string text, int sortOrder)
        {
            return new PollOptions
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                Text = text,
                ImageId = null,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static PollOptions CreateImage(Guid pollId, Guid imageId, int sortOrder)
        {
            return new PollOptions
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                Text = null,
                ImageId = imageId,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }
    }
}
