namespace Yakku.Application.Reasons.DTOs
{
    /// <summary>How a reason stands with moderation, as the owner sees it.</summary>
    public static class ReasonReportState
    {
        /// <summary>Never reported.</summary>
        public const string None = "none";

        /// <summary>Reported and waiting for an admin. Still shown, flagged.</summary>
        public const string Pending = "pending";

        /// <summary>Reported and an admin dismissed it. Shown as normal; it cannot be reported again.</summary>
        public const string Rejected = "rejected";
    }

    /// <summary>One reason a voter gave for an option. Anonymous: it carries no author.</summary>
    public class ReasonResponse
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        /// <summary>True when a registered user gave it, so the asker can chat back. False for a guest.</summary>
        public bool CanAskBack { get; set; }

        /// <summary>
        /// True when asking back on this reason needs the card to be unlocked first: the card is not unlocked, this
        /// is not the option's one free (top) reason, and the card's free ask back is gone or belongs to another
        /// reason. The app shows the unlock sheet instead of opening a chat.
        /// </summary>
        public bool AskBackLocked { get; set; }

        /// <summary>
        /// True when the owner cannot read this reason yet: the card is not revealed (no plan, no card purchase) and
        /// this is not the option's free top reason. <see cref="Text"/> is then empty; the app shows a blurred
        /// placeholder and the plans sheet. The text is never sent, so it cannot be read from the response.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>One of <see cref="ReasonReportState"/>: none, pending or rejected.</summary>
        public string ReportStatus { get; set; } = ReasonReportState.None;
    }

    /// <summary>The reasons for one option, plus how many were reported and blocked by an admin.</summary>
    public class ReasonListResponse
    {
        /// <summary>The reasons still visible, newest first. A reported reason waiting for an admin is included, flagged.</summary>
        public List<ReasonResponse> Items { get; set; } = [];

        /// <summary>How many of <see cref="Items"/> are hidden (<see cref="ReasonResponse.IsHidden"/>).</summary>
        public int HiddenCount { get; set; }

        /// <summary>Reasons for this option an admin approved a report on. Their text is never sent.</summary>
        public int BlockedCount { get; set; }
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

        /// <summary>Same as <see cref="ReasonResponse.AskBackLocked"/>; a custom answer's reason is always its own top reason.</summary>
        public bool AskBackLocked { get; set; }

        /// <summary>
        /// True when the owner cannot read this answer yet (free plan, not the one answer left open): <see cref="Answer"/>
        /// is empty and <see cref="Reason"/> is null. The text is never sent.
        /// </summary>
        public bool IsHidden { get; set; }

        /// <summary>
        /// One of <see cref="ReasonReportState"/> for the answer's <see cref="Reason"/>: none, pending or rejected.
        /// Always none when there is no reason.
        /// </summary>
        public string ReasonReportStatus { get; set; } = ReasonReportState.None;
    }

    /// <summary>The custom answers, plus how many reasons on them were reported and blocked by an admin.</summary>
    public class CustomAnswerListResponse
    {
        public List<CustomAnswerResponse> Items { get; set; } = [];

        /// <summary>How many of <see cref="Items"/> are hidden (<see cref="CustomAnswerResponse.IsHidden"/>).</summary>
        public int HiddenCount { get; set; }

        /// <summary>Reasons on custom answers an admin approved a report on. Their text is never sent.</summary>
        public int BlockedCount { get; set; }
    }

    /// <summary>What the owner sends to report a reason.</summary>
    public class ReportReasonRequest
    {
        /// <summary>
        /// Why the owner is reporting it, as text the client chooses (for example the label of the option they
        /// picked, or their own words). Required, up to 300 characters.
        /// </summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Returned after a reason is reported. The reason stays visible, flagged, until an admin decides, so the counts
    /// are unchanged by the report itself.
    /// </summary>
    public class ReasonReportResponse
    {
        public Guid ReasonId { get; set; }

        /// <summary>The option the reason was given for; null for a custom answer.</summary>
        public Guid? OptionId { get; set; }

        /// <summary>One of <see cref="ReasonReportState"/>: pending, or rejected when an admin had already dismissed it.</summary>
        public string Status { get; set; } = ReasonReportState.Pending;

        /// <summary>Reasons still counted for that option (the asker's own excluded).</summary>
        public int OptionWhyCount { get; set; }

        /// <summary>Reasons still counted across the whole poll or check.</summary>
        public int TotalWhyCount { get; set; }
    }
}
