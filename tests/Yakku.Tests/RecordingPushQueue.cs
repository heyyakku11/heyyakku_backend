using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;

namespace Yakku.Tests;

internal sealed class RecordingPushQueue : IPushQueue
{
    public List<VoteNotificationJob> Jobs { get; } = [];

    public bool Throw { get; set; }

    public ValueTask<bool> EnqueueAsync(VoteNotificationJob job, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("queue failure");
        }

        Jobs.Add(job);
        return ValueTask.FromResult(true);
    }
}
