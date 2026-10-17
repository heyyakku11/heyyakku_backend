namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One person's answer to a made check: either one of the check's options or a custom answer.
    /// The owner's own answer is recorded here too, when they make the check.
    /// </summary>
    public class UserCheckVote
    {
        public Guid Id { get; private set; }
        public Guid UserCheckId { get; private set; }
        public Guid CheckId { get; private set; }
        public Guid? CheckOptionId { get; private set; }
        public string? CustomAnswer { get; private set; }
        public Guid VoterId { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public UserCheck UserCheck { get; private set; } = null!;
        public CheckOption? CheckOption { get; private set; }
        public User Voter { get; private set; } = null!;

        private UserCheckVote()
        {
        }

        public static UserCheckVote ForOption(
            Guid userCheckId,
            Guid checkId,
            Guid checkOptionId,
            Guid voterId,
            string? reason)
        {
            return Create(userCheckId, checkId, checkOptionId, customAnswer: null, voterId, reason);
        }

        public static UserCheckVote ForCustomAnswer(
            Guid userCheckId,
            Guid checkId,
            string customAnswer,
            Guid voterId,
            string? reason)
        {
            return Create(userCheckId, checkId, checkOptionId: null, customAnswer, voterId, reason);
        }

        private static UserCheckVote Create(
            Guid userCheckId,
            Guid checkId,
            Guid? checkOptionId,
            string? customAnswer,
            Guid voterId,
            string? reason)
        {
            return new UserCheckVote
            {
                Id = Guid.NewGuid(),
                UserCheckId = userCheckId,
                CheckId = checkId,
                CheckOptionId = checkOptionId,
                CustomAnswer = customAnswer,
                VoterId = voterId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
