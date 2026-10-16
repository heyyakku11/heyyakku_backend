namespace Yakku.Domain.Enums
{
    /// <summary>Actual delivery outcome, only known when the provider reports it back.</summary>
    public enum NotificationDeliveryStatus
    {
        Unknown = 0,
        Delivered = 1,
        Failed = 2,
        NotApplicable = 3
    }
}
