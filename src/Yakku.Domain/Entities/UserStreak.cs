namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A user's streak summary. The row exists only once the user has had a qualifying activity, so
    /// <see cref="LastActivityDate"/> is never missing. <see cref="CurrentStreak"/> is the value as of that last
    /// activity; whether it is still running is decided at read time against today's date.
    /// </summary>
    public class UserStreak
    {
        public Guid UserId { get; private set; }
        public int CurrentStreak { get; private set; }
        public int LongestStreak { get; private set; }
        public DateOnly LastActivityDate { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User User { get; private set; } = null!;

        private UserStreak()
        {
        }
    }
}
