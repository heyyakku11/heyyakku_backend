namespace Yakku.Domain.Enums
{
    /// <summary>Where a reported reason is in moderation.</summary>
    public enum ReasonReportStatus
    {
        /// <summary>Reported, waiting for an admin. The reason stays visible to its owner, flagged as reported.</summary>
        Pending = 0,

        /// <summary>An admin upheld the report. The reason is removed from the vote and only counted as blocked.</summary>
        Approved = 1,

        /// <summary>An admin dismissed the report. The reason stays visible and is no longer flagged.</summary>
        Rejected = 2
    }
}
