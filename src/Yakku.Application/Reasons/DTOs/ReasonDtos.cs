namespace Yakku.Application.Reasons.DTOs
{
    /// <summary>One reason a voter gave for an option. Anonymous: it carries no author.</summary>
    public class ReasonResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
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
