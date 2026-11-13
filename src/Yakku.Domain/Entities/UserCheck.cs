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

        /// <summary>When the owner closed it so it stops taking answers; null while it is live.</summary>
        public DateTime? ClosedAt { get; private set; }

        /// <summary>
        /// When the card stops taking answers on its own. A card made from a fun card gets
        /// <see cref="FunCardLifetime"/> from the moment it is made, and from its close time when it is reshared.
        /// A made weekly check has no expiry (null).
        /// </summary>
        public DateTime? ExpiresAt { get; private set; }

        /// <summary>How long a fun card stays open after it is made, and after its close time when it is reshared.</summary>
        public static readonly TimeSpan FunCardLifetime = TimeSpan.FromHours(24);

        /// <summary>True when the owner closed it or its time ran out.</summary>
        public bool IsClosed => IsClosedAt(DateTime.UtcNow);

        public bool IsClosedAt(DateTime utcNow) =>
            ClosedAt is not null || (ExpiresAt is { } end && end <= utcNow);

        public User User { get; private set; } = null!;
        public Check Check { get; private set; } = null!;
        public List<UserCheckVote> Votes { get; private set; } = new();

        private UserCheck()
        {
        }

        /// <summary>Stops the check taking answers. False when it is already closed.</summary>
        public bool TryClose()
        {
            if (ClosedAt is not null)
            {
                return false;
            }

            ClosedAt = DateTime.UtcNow;
            return true;
        }

        /// <summary>
        /// Opens a closed card again so it takes answers and can be shared once more. A card that has an expiry gets
        /// a new one: <see cref="ClosedAt"/> plus <see cref="FunCardLifetime"/> (counted from now when it only ran out
        /// of time and has no close time). A card with no expiry is simply reopened. <see cref="ClosedAt"/> is cleared.
        /// False when the card is not closed.
        /// </summary>
        public bool TryReshare(DateTime utcNow)
        {
            if (!IsClosedAt(utcNow))
            {
                return false;
            }

            if (ExpiresAt is not null)
            {
                var closedAt = ClosedAt ?? utcNow;
                ExpiresAt = closedAt + FunCardLifetime;
            }

            ClosedAt = null;
            return true;
        }

        public static UserCheck Create(Guid userId, Guid checkId, string shareCode, DateTime? expiresAt = null)
        {
            return new UserCheck
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CheckId = checkId,
                ShareCode = shareCode,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt
            };
        }
    }
}
