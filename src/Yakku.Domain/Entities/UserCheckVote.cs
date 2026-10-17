namespace Yakku.Domain.Entities
{
    public class UserCheckVote
    {
        public Guid Id { get; private set; }
        public Guid UserCheckId { get; private set; }
        public Guid CheckId { get; private set; }
        public Guid CheckOptionId { get; private set; }
        public Guid VoterId { get; private set; }
        public string? Reason { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public UserCheck UserCheck { get; private set; } = null!;
        public CheckOption CheckOption { get; private set; } = null!;
        public User Voter { get; private set; } = null!;

        private UserCheckVote()
        {
        }

        public static UserCheckVote Create(
            Guid userCheckId,
            Guid checkId,
            Guid checkOptionId,
            Guid voterId,
            string? reason)
        {
            return new UserCheckVote
            {
                Id = Guid.NewGuid(),
                UserCheckId = userCheckId,
                CheckId = checkId,
                CheckOptionId = checkOptionId,
                VoterId = voterId,
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
