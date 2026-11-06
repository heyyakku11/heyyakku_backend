using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A user's personality card. Every user has exactly one, created at registration. The card owns its lifecycle
    /// (Draft, Collecting, Completed); callers never set the status or the vote counter directly.
    /// </summary>
    public class PersonalityCard
    {
        /// <summary>Votes a new card needs before it completes. Change here and redeploy; existing cards keep their own value.</summary>
        public const int DefaultRequiredVotes = 5;

        /// <summary>Traits picked for a self-assessment and for each vote.</summary>
        public const int TraitsPerSelection = 3;

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }

        /// <summary>The code in the card's share link. Unique across polls, checks and cards.</summary>
        public string ShareCode { get; private set; } = string.Empty;
        public PersonalityCardStatus Status { get; private set; }
        public int RequiredVotes { get; private set; }

        /// <summary>Maintained counter of persisted votes; the vote rows remain the source of truth.</summary>
        public int TotalVotes { get; private set; }

        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        public User User { get; private set; } = null!;
        public ICollection<PersonalityCardSelfTrait> SelfTraits { get; private set; } = new List<PersonalityCardSelfTrait>();
        public ICollection<PersonalityVote> Votes { get; private set; } = new List<PersonalityVote>();

        private PersonalityCard()
        {
        }

        public PersonalityCard(Guid userId, string? shareCode = null, int requiredVotes = DefaultRequiredVotes)
        {
            Id = Guid.NewGuid();
            UserId = userId;
            ShareCode = shareCode ?? ShareCodeGenerator.New();
            Status = PersonalityCardStatus.Draft;
            RequiredVotes = requiredVotes;
            TotalVotes = 0;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        /// <summary>Records the owner's three traits and opens the card for voting. Only valid while Draft.</summary>
        public void SubmitSelfAssessment(IReadOnlyCollection<Guid> traitIds)
        {
            if (Status != PersonalityCardStatus.Draft)
            {
                throw new InvalidOperationException("The self-assessment was already submitted.");
            }

            foreach (var traitId in traitIds)
            {
                SelfTraits.Add(new PersonalityCardSelfTrait(Id, traitId));
            }

            Status = PersonalityCardStatus.Collecting;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Counts one accepted vote and completes the card when the threshold is reached. Only valid while Collecting;
        /// the caller holds the row lock so the check and the increment cannot interleave with another vote.
        /// </summary>
        public void RegisterVote()
        {
            if (Status != PersonalityCardStatus.Collecting)
            {
                throw new InvalidOperationException("The card is not collecting votes.");
            }

            TotalVotes++;
            UpdatedAt = DateTime.UtcNow;
            if (TotalVotes >= RequiredVotes)
            {
                Status = PersonalityCardStatus.Completed;
                CompletedAt = UpdatedAt;
            }
        }
    }
}
