namespace Yakku.Domain.Entities
{
    public class PollCategory
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public Guid CategoryId { get; private set; }
        public DateTime AddedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public Category Category { get; private set; } = null!;

        private PollCategory()
        {
        }

        public PollCategory(Guid pollId, Guid categoryId)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            CategoryId = categoryId;
            AddedAt = DateTime.UtcNow;
        }
    }
}
