using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Sharing;

namespace Yakku.Tests;

internal sealed class RecordingVoteNotificationOutbox : IVoteNotificationOutbox
{
    public List<(VotableContent Content, VoteReceipt Receipt)> Recorded { get; } = [];

    public int Wakes { get; private set; }

    public bool Throw { get; set; }

    public Task RecordAsync(VotableContent content, VoteReceipt receipt, CancellationToken cancellationToken = default)
    {
        if (Throw)
        {
            throw new InvalidOperationException("outbox failure");
        }

        Recorded.Add((content, receipt));
        return Task.CompletedTask;
    }

    public void Wake() => Wakes++;
}
