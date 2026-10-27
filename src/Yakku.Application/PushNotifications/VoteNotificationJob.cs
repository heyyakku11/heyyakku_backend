namespace Yakku.Application.PushNotifications
{
    /// <summary>
    /// Carries ids only. The chosen option, custom answer and reason never enter the queue.
    /// </summary>
    public sealed record VoteNotificationJob(
        Guid OwnerId,
        Guid ContentId,
        bool IsPoll,
        string ShareCode,
        Guid VoteId);
}
