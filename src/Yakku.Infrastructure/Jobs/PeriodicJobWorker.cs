using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Jobs
{
    /// <summary>
    /// Runs a unit of work on a fixed interval in its own DI scope. A failing run is logged (and recorded in
    /// system_logs) and never stops the schedule.
    /// </summary>
    public abstract class PeriodicJobWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        private readonly string _jobName;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _interval;

        protected PeriodicJobWorker(
            IServiceScopeFactory scopeFactory,
            ILogger logger,
            string jobName,
            TimeSpan initialDelay,
            TimeSpan interval)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _jobName = jobName;
            _initialDelay = initialDelay;
            _interval = interval;
        }

        protected abstract Task RunAsync(IServiceProvider services, CancellationToken cancellationToken);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(_initialDelay, stoppingToken);
                using var timer = new PeriodicTimer(_interval);
                do
                {
                    await RunOnceAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        /// <summary>One guarded run. Exposed so a run can be triggered directly.</summary>
        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                await RunAsync(scope.ServiceProvider, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Background job {Job} did not finish ({ExceptionType}).",
                    _jobName,
                    exception.GetType().Name);

                await using var scope = _scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAuditLogWriter>().WriteSystemAsync(
                    new SystemLogEntry(
                        SystemLogCategory.BackgroundJob,
                        SystemLogSeverity.Error,
                        _jobName,
                        "Background job failed.",
                        ExceptionType: exception.GetType().Name,
                        Metadata: new Dictionary<string, string?> { ["job"] = _jobName }),
                    CancellationToken.None);
            }
        }
    }
}
