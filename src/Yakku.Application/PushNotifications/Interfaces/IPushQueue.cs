namespace Yakku.Application.PushNotifications.Interfaces
{
    public interface IPushQueue
    {
        /// <summary>
        /// Queues a job. Returns false when the queue stayed full past the enqueue timeout and the job was dropped.
        /// Never throws, so a notification problem cannot fail the caller.
        /// </summary>
        ValueTask<bool> EnqueueAsync(VoteNotificationJob job, CancellationToken cancellationToken = default);
    }
}
