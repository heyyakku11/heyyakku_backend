namespace Yakku.Domain.Entities
{
    /// <summary>One user's perception of a card owner: exactly three traits. Immutable once saved.</summary>
    public class PersonalityVote
    {
        public Guid Id { get; private set; }
        public Guid PersonalityCardId { get; private set; }
        public Guid VoterUserId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public PersonalityCard PersonalityCard { get; private set; } = null!;
        public User Voter { get; private set; } = null!;
        public ICollection<PersonalityVoteTrait> Traits { get; private set; } = new List<PersonalityVoteTrait>();

        private PersonalityVote()
        {
        }

        public PersonalityVote(Guid personalityCardId, Guid voterUserId, IEnumerable<int> traitIds)
        {
            Id = Guid.NewGuid();
            PersonalityCardId = personalityCardId;
            VoterUserId = voterUserId;
            CreatedAt = DateTime.UtcNow;
            foreach (var traitId in traitIds)
            {
                Traits.Add(new PersonalityVoteTrait(Id, traitId));
            }
        }
    }

    /// <summary>One trait picked in a <see cref="PersonalityVote"/>.</summary>
    public class PersonalityVoteTrait
    {
        public Guid Id { get; private set; }
        public Guid PersonalityVoteId { get; private set; }
        public int TraitId { get; private set; }

        public PersonalityVote PersonalityVote { get; private set; } = null!;
        public PersonalityTrait Trait { get; private set; } = null!;

        private PersonalityVoteTrait()
        {
        }

        internal PersonalityVoteTrait(Guid personalityVoteId, int traitId)
        {
            Id = Guid.NewGuid();
            PersonalityVoteId = personalityVoteId;
            TraitId = traitId;
        }
    }
}
