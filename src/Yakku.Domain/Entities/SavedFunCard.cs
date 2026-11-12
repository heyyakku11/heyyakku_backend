namespace Yakku.Domain.Entities
{
    /// <summary>A just-for-fun card a user saved to their list. Only fun cards can be saved this way.</summary>
    public class SavedFunCard
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid CheckId { get; private set; }
        public DateTime SavedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Check Check { get; private set; } = null!;

        private SavedFunCard()
        {
        }

        public SavedFunCard(Guid userId, Guid checkId)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            CheckId = checkId;
            SavedAt = DateTime.UtcNow;
        }
    }
}
