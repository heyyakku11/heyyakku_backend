using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common;

namespace Yakku.Infrastructure.Jobs
{
    public sealed class SessionCleanupWorker : PeriodicJobWorker
    {
        public SessionCleanupWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<SessionCleanupWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.SessionCleanup,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.Sessions.CleanupInitialDelayMinutes),
                interval: TimeSpan.FromMinutes(OperationalSettings.Sessions.CleanupIntervalMinutes))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await services.GetRequiredService<ISessionCleanupService>().CleanupAsync(cancellationToken);
        }
    }
}
