using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yakku.Application.AskBack;
using Yakku.Application.Audit;

namespace Yakku.Infrastructure.Jobs
{
    /// <summary>Runs <see cref="AskBackDeliveryTimeoutSweeper"/> on the configured interval.</summary>
    public sealed class AskBackDeliveryTimeoutWorker : PeriodicJobWorker
    {
        public AskBackDeliveryTimeoutWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<AskBackDeliveryOptions> options,
            ILogger<AskBackDeliveryTimeoutWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.AskBackDeliveryTimeout,
                initialDelay: options.Value.SweepInterval,
                interval: options.Value.SweepInterval)
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await services.GetRequiredService<AskBackDeliveryTimeoutSweeper>().SweepAsync(cancellationToken);
        }
    }
}
