namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Maps a user to a check they used. A user uses a check at most once; a check can be used by many users
    /// and a user can use many checks. The share code lets others open and answer it.
    /// </summary>
    public class UserCheck
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid CheckId { get; private set; }
        public string ShareCode { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Check Check { get; private set; } = null!;
        public List<UserCheckVote> Votes { get; private set; } = new();

        private UserCheck()
        {
        }

        public static UserCheck Create(Guid userId, Guid checkId, string shareCode)
        {
            return new UserCheck
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CheckId = checkId,
                ShareCode = shareCode,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
