namespace Yakku.Domain.Entities
{
    public class SavedPoll
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid PollId { get; private set; }
        public DateTime SavedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Poll Poll { get; private set; } = null!;

        private SavedPoll()
        {
        }

        public SavedPoll(Guid userId, Guid pollId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            PollId = pollId;
            SavedAt = DateTime.UtcNow;
        }
    }
}
