using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;

namespace Yakku.Infrastructure.Push
{
    public sealed class PushQueue : IPushQueue
    {
        private readonly ChannelWriter<VoteNotificationJob> _writer;
        private readonly ILogger<PushQueue> _logger;

        public PushQueue(Channel<VoteNotificationJob> channel, ILogger<PushQueue> logger)
        {
            _writer = channel.Writer;
            _logger = logger;
        }

        public async ValueTask<bool> EnqueueAsync(VoteNotificationJob job, CancellationToken cancellationToken = default)
        {
            if (_writer.TryWrite(job))
            {
                return true;
            }

            // The queue is full: wait a short while for the consumer to catch up (backpressure), then give up so
            // the caller is never held for long. The vote is already committed.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(PushQueueOptions.EnqueueTimeout);

            try
            {
                await _writer.WriteAsync(job, timeout.Token);
                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Push queue is full; dropped vote notification for owner {OwnerId}, content {ContentId}.",
                    job.OwnerId,
                    job.ContentId);
                return false;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }
    }
}
