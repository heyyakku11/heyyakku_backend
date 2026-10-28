using Yakku.Domain.Enums;

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

        /// <summary>A user id or a guest id, as told by <see cref="VoterRole"/>. Not a foreign key.</summary>
        public Guid VoterId { get; private set; }
        public VoterRole VoterRole { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public UserCheck UserCheck { get; private set; } = null!;
        public CheckOption? CheckOption { get; private set; }

        private UserCheckVote()
        {
        }

        /// <summary>Drops the reason text (a reported reason); the vote itself stays.</summary>
        public void RemoveReason()
        {
            Reason = null;
        }

        public static UserCheckVote ForOption(
            Guid userCheckId,
            Guid checkId,
            Guid checkOptionId,
            Guid voterId,
            string? reason,
            VoterRole voterRole = VoterRole.User)
        {
            return Create(userCheckId, checkId, checkOptionId, customAnswer: null, voterId, reason, voterRole);
        }

        public static UserCheckVote ForCustomAnswer(
            Guid userCheckId,
            Guid checkId,
            string customAnswer,
            Guid voterId,
            string? reason,
            VoterRole voterRole = VoterRole.User)
        {
            return Create(userCheckId, checkId, checkOptionId: null, customAnswer, voterId, reason, voterRole);
        }

        private static UserCheckVote Create(
            Guid userCheckId,
            Guid checkId,
            Guid? checkOptionId,
            string? customAnswer,
            Guid voterId,
            string? reason,
            VoterRole voterRole)
        {
            return new UserCheckVote
            {
                Id = Guid.NewGuid(),
                UserCheckId = userCheckId,
                CheckId = checkId,
                CheckOptionId = checkOptionId,
                CustomAnswer = customAnswer,
                VoterId = voterId,
                VoterRole = voterRole,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
