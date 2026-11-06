namespace Yakku.Application.Personality.DTOs
{
    /// <summary>A predefined trait a user can pick.</summary>
    public class PersonalityTraitResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Exactly three distinct trait ids. Used for both the self-assessment and a vote.</summary>
    public class SubmitTraitsRequest
    {
        public List<Guid> TraitIds { get; set; } = new();
    }

    public class PersonalityTraitSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The caller's own card. The status and counters are owned by the backend.</summary>
    public class PersonalityCardResponse
    {
        public Guid CardId { get; set; }
        public string ShareCode { get; set; } = string.Empty;

        /// <summary>Draft, Collecting or Completed.</summary>
        public string Status { get; set; } = string.Empty;
        public List<PersonalityTraitSummary> SelfTraits { get; set; } = new();
        public int TotalVotes { get; set; }
        public int RequiredVotes { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>A card as a voter sees it: no self-assessment (it would bias the vote) and no voter details.</summary>
    public class SharedPersonalityCardResponse
    {
        public Guid CardId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string OwnerAnonymousName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public bool HasVoted { get; set; }

        /// <summary>True when the caller could vote right now.</summary>
        public bool CanVote { get; set; }
        public int TotalVotes { get; set; }
        public int RequiredVotes { get; set; }
    }

    public class PersonalityVoteResponse
    {
        public Guid CardId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string CardStatus { get; set; } = string.Empty;
        public int TotalVotes { get; set; }
        public int RequiredVotes { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class PersonalityTraitCountResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        /// <summary>How many voters picked this trait. An aggregate only: voters are never identified.</summary>
        public int Votes { get; set; }
    }

    /// <summary>"You vs the crowd": the owner's three traits against the crowd's top three.</summary>
    public class PersonalityResultsResponse
    {
        public Guid CardId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public int TotalVotes { get; set; }
        public List<PersonalityTraitSummary> SelfSelectedTraits { get; set; } = new();
        public List<PersonalityTraitCountResponse> CrowdTopTraits { get; set; } = new();
        public List<PersonalityTraitSummary> CommonTraits { get; set; } = new();
        public List<PersonalityTraitSummary> SelfOnlyTraits { get; set; } = new();
        public List<PersonalityTraitSummary> CrowdOnlyTraits { get; set; } = new();
    }
}
