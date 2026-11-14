using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications.Batching;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Runs the vote notification pipeline: classifies new votes, sends individual pushes, polls the Redis schedule
    /// for due batches (every <c>SchedulerPollIntervalMs</c> while a batch is waiting) and sweeps the database for
    /// work Redis could not hand out. It sleeps until a vote wakes it or the poll interval passes, and keeps going
    /// while there is work. Everything it needs is in PostgreSQL or Redis, so a restart loses nothing.
    /// </summary>
    public sealed class VoteNotificationWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly VoteNotificationSignal _signal;
        private readonly VoteNotificationBatchingOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<VoteNotificationWorker> _logger;

        public VoteNotificationWorker(
            IServiceScopeFactory scopeFactory,
            VoteNotificationSignal signal,
            VoteNotificationBatchingOptions options,
            TimeProvider time,
            ILogger<VoteNotificationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _signal = signal;
            _options = options;
            _time = time;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var sweepEvery = TimeSpan.FromSeconds(_options.RecoverySweepIntervalSeconds);
            var idleRedisPollEvery = TimeSpan.FromSeconds(_options.IdleRedisPollSeconds);
            var pollInterval = TimeSpan.FromMilliseconds(_options.SchedulerPollIntervalMs);

            var lastSweep = DateTimeOffset.MinValue;
            var lastRedisPoll = DateTimeOffset.MinValue;
            var queueSize = 0L;
            var woken = true;
            var busy = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var now = _time.GetUtcNow();
                    var sweepDue = now - lastSweep >= sweepEvery;
                    var runDatabase = woken || busy || sweepDue;
                    var runRedis = woken || busy || queueSize > 0 || now - lastRedisPoll >= idleRedisPollEvery;
                    var handled = 0;

                    try
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        var services = scope.ServiceProvider;

                        if (runDatabase)
                        {
                            handled += await services
                                .GetRequiredService<VoteEventClassifier>()
                                .ProcessAsync(stoppingToken);
                            handled += await services
                                .GetRequiredService<VoteNotificationDispatcher>()
                                .ProcessImmediateAsync(stoppingToken);
                        }

                        var batches = services.GetRequiredService<VoteBatchDispatcher>();
                        if (runRedis)
                        {
                            var (scheduled, size) = await batches.ProcessScheduleAsync(stoppingToken);
                            handled += scheduled;
                            queueSize = size;
                            lastRedisPoll = now;
                        }

                        if (sweepDue)
                        {
                            handled += await batches.SweepAsync(stoppingToken);
                            lastSweep = now;
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Vote notification dispatch run failed.");
                    }

                    busy = handled > 0;
                    woken = false;
                    if (!busy)
                    {
                        woken = await _signal.WaitAsync(pollInterval, stoppingToken);
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
