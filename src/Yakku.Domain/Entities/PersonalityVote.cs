namespace Yakku.Domain.Entities
{
    /// <summary>
    /// One voter's perception of a card owner: exactly three traits. Immutable once saved. The voter is a signed-in
    /// user or a guest (exactly one of the two ids is set).
    /// </summary>
    public class PersonalityVote
    {
        public Guid Id { get; private set; }
        public Guid PersonalityCardId { get; private set; }
        public Guid? VoterUserId { get; private set; }
        public Guid? VoterGuestId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public PersonalityCard PersonalityCard { get; private set; } = null!;
        public User? Voter { get; private set; }
        public Guest? VoterGuest { get; private set; }
        public ICollection<PersonalityVoteTrait> Traits { get; private set; } = new List<PersonalityVoteTrait>();

        private PersonalityVote()
        {
        }

        public PersonalityVote(Guid personalityCardId, Guid voterId, bool isGuest, IEnumerable<Guid> traitIds)
        {
            Id = Guid.NewGuid();
            PersonalityCardId = personalityCardId;
            if (isGuest)
            {
                VoterGuestId = voterId;
            }
            else
            {
                VoterUserId = voterId;
            }
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
        public Guid TraitId { get; private set; }

        public PersonalityVote PersonalityVote { get; private set; } = null!;
        public PersonalityTrait Trait { get; private set; } = null!;

        private PersonalityVoteTrait()
        {
        }

        internal PersonalityVoteTrait(Guid personalityVoteId, Guid traitId)
        {
            Id = Guid.NewGuid();
            PersonalityVoteId = personalityVoteId;
            TraitId = traitId;
        }
    }
}
