using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Common;

namespace Yakku.Infrastructure.Jobs
{
    public sealed class GuestCleanupWorker : PeriodicJobWorker
    {
        public GuestCleanupWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<GuestCleanupWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.GuestCleanup,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.Guests.CleanupInitialDelayMinutes),
                interval: TimeSpan.FromMinutes(OperationalSettings.Guests.CleanupIntervalMinutes))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await services.GetRequiredService<IGuestCleanupService>().CleanupAsync(cancellationToken);
        }
    }
}
