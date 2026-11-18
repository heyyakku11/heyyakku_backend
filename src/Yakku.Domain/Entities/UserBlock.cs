namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One user blocked another from an Ask Back conversation. The blocked user can no longer vote on the blocker's
    /// polls, checks or personality card, and the two cannot start a new Ask Back. It is never announced.
    /// </summary>
    public class UserBlock
    {
        public Guid Id { get; private set; }
        public Guid BlockerUserId { get; private set; }
        public Guid BlockedUserId { get; private set; }

        /// <summary>The conversation the block came from. Not a foreign key, so deleting a chat keeps the block.</summary>
        public Guid? SourceConversationId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private UserBlock()
        {
        }

        public static UserBlock Create(
            Guid blockerUserId,
            Guid blockedUserId,
            Guid? sourceConversationId,
            DateTime utcNow)
        {
            if (blockerUserId == blockedUserId)
            {
                throw new ArgumentException("A user cannot block themselves.", nameof(blockedUserId));
            }

            return new UserBlock
            {
                Id = Guid.NewGuid(),
                BlockerUserId = blockerUserId,
                BlockedUserId = blockedUserId,
                SourceConversationId = sourceConversationId,
                CreatedAt = utcNow
            };
        }
    }
}
