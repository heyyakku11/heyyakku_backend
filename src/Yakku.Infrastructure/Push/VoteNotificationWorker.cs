using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Polls the vote notification outbox. It sleeps until a vote wakes it or the poll interval passes, and keeps
    /// going while there is work. Nothing is held in memory, so a restart loses nothing.
    /// </summary>
    public sealed class VoteNotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly VoteNotificationSignal _signal;
        private readonly ILogger<VoteNotificationWorker> _logger;

        public VoteNotificationWorker(
            IServiceScopeFactory scopeFactory,
            VoteNotificationSignal signal,
            ILogger<VoteNotificationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _signal = signal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var handled = 0;
                    try
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var dispatcher = scope.ServiceProvider.GetRequiredService<VoteNotificationDispatcher>();
                        handled += await dispatcher.ProcessImmediateAsync(stoppingToken);
                        handled += await dispatcher.ProcessWindowsAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Vote notification dispatch run failed.");
                    }

                    if (handled == 0)
                    {
                        await _signal.WaitAsync(VoteNotificationOptions.PollInterval, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host is stopping: leave quietly.
            }
        }
    }
}
