using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A card a user saved, in one place whatever its kind. <see cref="CardType"/> says whether it is a poll or a
    /// fun card; exactly one of <see cref="PollId"/> and <see cref="FunCardId"/> is set to match.
    /// </summary>
    public class SavedCard
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public SavedCardType CardType { get; private set; }

        /// <summary>The saved poll; set only when <see cref="CardType"/> is <see cref="SavedCardType.Poll"/>.</summary>
        public Guid? PollId { get; private set; }

        /// <summary>The saved fun card (a check of kind Fun); set only when <see cref="CardType"/> is <see cref="SavedCardType.FunCard"/>.</summary>
        public Guid? FunCardId { get; private set; }

        public DateTime SavedAt { get; private set; }

        public User User { get; private set; } = null!;
        public Poll? Poll { get; private set; }
        public Check? Check { get; private set; }

        /// <summary>The id of the saved card, whichever kind it is.</summary>
        public Guid CardId => CardType == SavedCardType.Poll ? PollId!.Value : FunCardId!.Value;

        private SavedCard()
        {
        }

        public static SavedCard ForPoll(Guid userId, Guid pollId) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardType = SavedCardType.Poll,
            PollId = pollId,
            SavedAt = DateTime.UtcNow
        };

        public static SavedCard ForFunCard(Guid userId, Guid funCardId) => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CardType = SavedCardType.FunCard,
            FunCardId = funCardId,
            SavedAt = DateTime.UtcNow
        };
    }
}
