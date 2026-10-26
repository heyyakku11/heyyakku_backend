namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One row per user per UTC calendar day on which the user did something that counts toward a streak.
    /// The authoritative streak history: the summary in <see cref="UserStreak"/> can always be rebuilt from it.
    /// </summary>
    public class UserActivityDay
    {
        public Guid UserId { get; private set; }

        /// <summary>The UTC calendar day.</summary>
        public DateOnly ActivityDate { get; private set; }

        /// <summary>When the first qualifying activity of that day happened (UTC).</summary>
        public DateTime FirstActivityAt { get; private set; }

        public DateTime CreatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private UserActivityDay()
        {
        }
    }
}
