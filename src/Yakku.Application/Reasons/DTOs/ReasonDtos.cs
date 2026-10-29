namespace Yakku.Application.Reasons.DTOs
{
    /// <summary>One reason a voter gave for an option. Anonymous: it carries no author.</summary>
    public class ReasonResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>True when a registered user gave it, so the asker can chat back. False for a guest.</summary>
        public bool CanAskBack { get; set; }
    }

    /// <summary>A custom answer a voter wrote in ("their own option"), with the reason they gave, if any. Anonymous.</summary>
    public class CustomAnswerResponse
    {
        /// <summary>The vote id. Also the id to report when <see cref="Reason"/> is set.</summary>
        public Guid Id { get; set; }
        public string Answer { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>True when a registered user wrote it, so the asker can chat back. False for a guest.</summary>
        public bool CanAskBack { get; set; }
    }

    /// <summary>
    /// Returned after a reason is reported. The reported reason no longer counts: these are the reason counts as
    /// they are now, so the client can update without reloading.
    /// </summary>
    public class ReasonReportResponse
    {
        public Guid ReasonId { get; set; }

        /// <summary>The option the reason was given for; null for a custom answer.</summary>
        public Guid? OptionId { get; set; }

        /// <summary>Reasons still counted for that option (the asker's own and reported ones excluded).</summary>
        public int OptionWhyCount { get; set; }

        /// <summary>Reasons still counted across the whole poll or check.</summary>
        public int TotalWhyCount { get; set; }
    }
}
