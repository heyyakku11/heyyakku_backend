namespace Yakku.Application.Checks.DTOs
{
    public class CheckOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>An entry of the live checks list. <see cref="Used"/> is per caller.</summary>
    public class CheckResponse
    {
        public Guid Id { get; set; }

        /// <summary>The Monday of the week this check went live.</summary>
        public DateOnly WeekStart { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
        public List<CheckOptionResponse> Options { get; set; } = new();
        public bool Used { get; set; }
    }

    public class UserCheckOptionResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;

        /// <summary>Null until the caller is allowed to see results (the owner, or after voting).</summary>
        public int? Votes { get; set; }

        /// <summary>Number of voters who gave a reason for this option. Null while results are hidden.</summary>
        public int? WhyCount { get; set; }
    }

    /// <summary>A check a user made, as seen by its owner or by someone opening the share link.</summary>
    public class UserCheckResponse
    {
        public Guid Id { get; set; }
        public Guid CheckId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
        public string ShareCode { get; set; } = string.Empty;
        public bool IsOwner { get; set; }
        public Guid? MyVoteOptionId { get; set; }
        public string? MyCustomAnswer { get; set; }
        public int? TotalVotes { get; set; }

        /// <summary>Votes needed from others before results are revealed.</summary>
        public int MinVotes { get; set; }

        /// <summary>Votes from others so far (the owner's own answer is not counted).</summary>
        public int VoteProgress { get; set; }

        /// <summary>True once <see cref="VoteProgress"/> reaches <see cref="MinVotes"/>.</summary>
        public bool ResultsUnlocked { get; set; }

        /// <summary>How many answers were written in instead of picking an option. Null until results are visible.</summary>
        public int? CustomAnswerVotes { get; set; }
        public List<UserCheckOptionResponse> Options { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Admin create/update body. weekStart may be any date; it is snapped to that week's Monday.</summary>
    public class SaveCheckRequest
    {
        public DateOnly WeekStart { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public class AdminCheckResponse
    {
        public Guid Id { get; set; }
        public DateOnly WeekStart { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = string.Empty;
        public string Sticker { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>live = this week or earlier, upcoming = a later week.</summary>
        public string Status { get; set; } = string.Empty;
        public List<CheckOptionResponse> Options { get; set; } = new();
    }

    /// <summary>An answer to a check: exactly one of checkOptionId or customAnswer, plus an optional reason.</summary>
    public class CastCheckVoteRequest
    {
        public Guid? CheckOptionId { get; set; }
        public string? CustomAnswer { get; set; }
        public string? Reason { get; set; }
    }

    /// <summary>Body of POST /checks/make: the check being made plus the maker's own answer to it.</summary>
    public class MakeCheckRequest
    {
        public Guid CheckId { get; set; }
        public Guid? CheckOptionId { get; set; }
        public string? Reason { get; set; }
        public string? CustomAnswer { get; set; }
    }

    public class CheckVoteResponse
    {
        public Guid Id { get; set; }
        public Guid UserCheckId { get; set; }
        public Guid? CheckOptionId { get; set; }
        public string? CustomAnswer { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
