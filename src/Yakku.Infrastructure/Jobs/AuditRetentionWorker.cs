using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Infrastructure.Audit;
using Yakku.Application.Common;

namespace Yakku.Infrastructure.Jobs
{
    public sealed class AuditRetentionWorker : PeriodicJobWorker
    {
        public AuditRetentionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<AuditRetentionWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.AuditRetention,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.Retention.InitialDelayMinutes),
                interval: TimeSpan.FromHours(OperationalSettings.Retention.IntervalHours))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await services.GetRequiredService<AuditRetentionService>().PurgeAsync(DateTime.UtcNow, cancellationToken);
        }
    }
}
