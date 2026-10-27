using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Push
{
    public sealed class VoteNotificationWorker : BackgroundService
    {
        private readonly ChannelReader<VoteNotificationJob> _reader;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VoteNotificationWorker> _logger;

        public VoteNotificationWorker(
            Channel<VoteNotificationJob> channel,
            IServiceScopeFactory scopeFactory,
            ILogger<VoteNotificationWorker> logger)
        {
            _reader = channel.Reader;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var job in _reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await ProcessJobAsync(job, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Unhandled fault while processing vote notification for owner {OwnerId}, vote {VoteId}",
                            job.OwnerId,
                            job.VoteId);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is stopping: leave quietly.
            }
        }

        private async Task ProcessJobAsync(VoteNotificationJob job, CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

            // Saved once, before the retry loop, so retries can never create a second row.
            var noun = job.IsPoll ? "poll" : "check";
            var notification = new Notification(
                job.OwnerId,
                NotificationType.Normal,
                NotificationEventType.PollAnswered,
                "New answer",
                $"Someone answered your {noun}.",
                data: JsonSerializer.Serialize(new
                {
                    contentId = job.ContentId,
                    contentType = noun,
                    shareCode = job.ShareCode
                }));
            await notifications.AddAsync(notification, stoppingToken);
            await notifications.SaveChangesAsync(stoppingToken);

            var payload = PushPayloads.ForNotification(notification);

            for (var attempt = 1; attempt <= PushQueueOptions.MaxAttempts; attempt++)
            {
                stoppingToken.ThrowIfCancellationRequested();

                var retryable = false;
                try
                {
                    var result = await push.SendToUserAsync(job.OwnerId, payload, stoppingToken);

                    // Retry only when nothing reached the owner. After a partial success a retry would send a
                    // second push to the devices that already got one.
                    retryable = result.Successful == 0 && result.RetryableFailures > 0;
                    if (!retryable)
                    {
                        return;
                    }
                }
                catch (AppException exception) when (exception.StatusCode is 400 or 404)
                {
                    // No devices, or none eligible (inactive, push not allowed, no live session). Nothing to retry.
                    _logger.LogInformation(
                        "No push-eligible device for owner {OwnerId}; skipping vote push.",
                        job.OwnerId);
                    return;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // Provider or transport fault (e.g. a 502 from the Firebase wrapper).
                    retryable = true;
                    _logger.LogWarning(
                        exception,
                        "Vote push attempt {Attempt} failed for owner {OwnerId}.",
                        attempt,
                        job.OwnerId);
                }

                if (attempt >= PushQueueOptions.MaxAttempts)
                {
                    _logger.LogWarning(
                        "Vote push permanently failed for owner {OwnerId} after {Attempts} attempts.",
                        job.OwnerId,
                        attempt);
                    return;
                }

                await Task.Delay(
                    PushQueueOptions.BackoffWithJitter(attempt, Random.Shared.NextDouble()),
                    stoppingToken);
            }
        }
    }
}
