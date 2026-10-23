using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Sharing.DTOs
{
    public static class ContentTypes
    {
        public const string Poll = "POLL";
        public const string Check = "CHECK";
    }

    /// <summary>One shape for a poll or a made check: content, options, the caller's voting state and permitted statistics.</summary>
    public class SharedContentResponse
    {
        /// <summary>POLL or CHECK.</summary>
        public string ContentType { get; set; } = string.Empty;

        public Guid ContentId { get; set; }
        public string ShareCode { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsOwner { get; set; }
        public List<SharedOptionResponse> Options { get; set; } = [];
        public VotingStateResponse Voting { get; set; } = new();
        public VoteStatsResponse Stats { get; set; } = new();

        /// <summary>Only for polls.</summary>
        public PollDetailsResponse? Poll { get; set; }

        /// <summary>Only for checks.</summary>
        public CheckDetailsResponse? Check { get; set; }
    }

    public class SharedOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }

        /// <summary>Null unless results are visible to this caller.</summary>
        public int? VoteCount { get; set; }

        /// <summary>Share of all votes, 0 to 100 with two decimals. Null unless results are visible.</summary>
        public decimal? Percentage { get; set; }

        public int? WhyCount { get; set; }

        /// <summary>True for the option this caller voted for (always shown to the voter).</summary>
        public bool IsSelected { get; set; }
    }

    public class VotingStateResponse
    {
        /// <summary>False when the caller already voted, the content is closed or the caller is the owner of a check.</summary>
        public bool CanVote { get; set; }

        public bool HasVoted { get; set; }

        /// <summary>ALREADY_VOTED, OWNER, CLOSED, INACTIVE or EXPIRED. Null when CanVote.</summary>
        public string? BlockedReason { get; set; }

        public MyVoteResponse? MyVote { get; set; }
    }

    public class MyVoteResponse
    {
        public Guid? OptionId { get; set; }
        public string? CustomAnswer { get; set; }
        public string? Reason { get; set; }
        public DateTime VotedAt { get; set; }
    }

    public class VoteStatsResponse
    {
        /// <summary>Counts and percentages are included only when true: unlocked, and the caller is the owner or has voted.</summary>
        public bool ResultsVisible { get; set; }

        public int MinVotesToReveal { get; set; }

        /// <summary>Votes from people other than the owner; this is what unlocks the results.</summary>
        public int VoteProgress { get; set; }

        public int? TotalVotes { get; set; }
        public int? CustomAnswerCount { get; set; }
        public decimal? CustomAnswerPercentage { get; set; }
        public int? WhyCount { get; set; }
    }

    public class PollDetailsResponse
    {
        public string PollType { get; set; } = string.Empty;
        public PollMediaResponse? Media { get; set; }
    }

    public class CheckDetailsResponse
    {
        public Guid CheckId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
    }

    /// <summary>Votes on the poll or made check that <see cref="ShareCode"/> resolves to.</summary>
    public class VoteRequest
    {
        public string? ShareCode { get; set; }

        /// <summary>Exactly one of OptionId or CustomAnswer.</summary>
        public Guid? OptionId { get; set; }

        public string? CustomAnswer { get; set; }
        public string? Reason { get; set; }
    }
}
