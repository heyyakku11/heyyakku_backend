namespace Yakku.Domain.Enums
{
    public enum EmailStatus
    {
        Queued = 0,
        Sending = 1,
        Sent = 2,
        Failed = 3,
        Cancelled = 4
    }
}
