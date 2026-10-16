namespace Yakku.Domain.Enums
{
    /// <summary>What the provider told us when we handed the message over. Not proof of delivery.</summary>
    public enum NotificationAuditStatus
    {
        Queued = 0,
        Accepted = 1,
        Failed = 2,
        Skipped = 3
    }
}
