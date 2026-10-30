using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.AskBack;

namespace Yakku.Infrastructure.Push
{
    /// <summary>Polls the Ask Back push rows. Nothing is held in memory, so a restart loses nothing.</summary>
    public sealed class AskBackPushWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AskBackPushWorker> _logger;

        public AskBackPushWorker(IServiceScopeFactory scopeFactory, ILogger<AskBackPushWorker> logger)
        {
            _scopeFactory = scopeFactory;
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
                        var dispatcher = scope.ServiceProvider.GetRequiredService<AskBackPushDispatcher>();
                        handled = await dispatcher.ProcessAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Ask Back push run failed.");
                    }

                    if (handled == 0)
                    {
                        await Task.Delay(AskBackOptions.PollInterval, stoppingToken);
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
