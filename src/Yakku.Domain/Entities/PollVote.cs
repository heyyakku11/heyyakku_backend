using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class PollVote
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        /// <summary>A user id or a guest id, as told by <see cref="VoterRole"/>. Not a foreign key.</summary>
        public Guid VoterId { get; private set; }
        public VoterRole VoterRole { get; private set; }
        public Guid? PollOptionId { get; private set; }
        public string? CustomAnswer { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;
        public PollOption? PollOption { get; private set; }

        private PollVote()
        {
        }

        /// <summary>Drops the reason text (a reported reason); the vote itself stays.</summary>
        public void RemoveReason()
        {
            Reason = null;
            UpdatedAt = DateTime.UtcNow;
        }

        public static PollVote ForOption(
            Guid voterId,
            Guid pollId,
            Guid pollOptionId,
            string? reason,
            VoterRole voterRole = VoterRole.User)
        {
            return Create(voterId, voterRole, pollId, pollOptionId, customAnswer: null, reason);
        }

        public static PollVote ForCustomAnswer(
            Guid voterId,
            Guid pollId,
            string customAnswer,
            string? reason,
            VoterRole voterRole = VoterRole.User)
        {
            return Create(voterId, voterRole, pollId, pollOptionId: null, customAnswer, reason);
        }

        private static PollVote Create(
            Guid voterId,
            VoterRole voterRole,
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
                VoterRole = voterRole,
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
