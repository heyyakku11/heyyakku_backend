namespace Yakku.Domain.Entities
{
    /// <summary>One of the three traits the card owner chose to describe themselves.</summary>
    public class PersonalityCardSelfTrait
    {
        public Guid Id { get; private set; }
        public Guid PersonalityCardId { get; private set; }
        public int TraitId { get; private set; }

        public PersonalityCard PersonalityCard { get; private set; } = null!;
        public PersonalityTrait Trait { get; private set; } = null!;

        private PersonalityCardSelfTrait()
        {
        }

        internal PersonalityCardSelfTrait(Guid personalityCardId, int traitId)
        {
            Id = Guid.NewGuid();
            PersonalityCardId = personalityCardId;
            TraitId = traitId;
        }
    }
}
