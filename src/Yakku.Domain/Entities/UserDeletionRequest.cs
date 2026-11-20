namespace Yakku.Domain.Entities
{
    /// <summary>Ticket created when a user schedules account deletion.</summary>
    public class UserDeletionRequest
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public DateTime RequestedAt { get; private set; }
        public DateTime ScheduledFor { get; private set; }

        public User User { get; private set; } = null!;

        private UserDeletionRequest()
        {
        }

        public static UserDeletionRequest Create(Guid userId, DateTime utcNow, TimeSpan gracePeriod)
        {
            return new UserDeletionRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                RequestedAt = utcNow,
                ScheduledFor = utcNow + gracePeriod
            };
        }
    }
}
