namespace Yakku.Domain.Entities
{
    public class PollVote
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public Guid VoterId { get; private set; }
        public Guid? PollOptionId { get; private set; }
        public string? CustomAnswer { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public User Voter { get; private set; } = null!;
        public PollOption? PollOption { get; private set; }

        private PollVote()
        {
        }

        public static PollVote ForOption(
            Guid voterId,
            Guid pollId,
            Guid pollOptionId,
            string? reason)
        {
            return Create(voterId, pollId, pollOptionId, customAnswer: null, reason);
        }

        public static PollVote ForCustomAnswer(
            Guid voterId,
            Guid pollId,
            string customAnswer,
            string? reason)
        {
            return Create(voterId, pollId, pollOptionId: null, customAnswer, reason);
        }

        private static PollVote Create(
            Guid voterId,
            Guid pollId,
            Guid? pollOptionId,
            string? customAnswer,
            string? reason)
        {
            var now = DateTime.UtcNow;
            return new PollVote
            {
                Id = Guid.NewGuid(),
                VoterId = voterId,
                PollId = pollId,
                PollOptionId = pollOptionId,
                CustomAnswer = customAnswer,
                Reason = reason,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
