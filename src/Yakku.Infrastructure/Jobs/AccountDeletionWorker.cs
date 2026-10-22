using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Common;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Infrastructure.Jobs
{
    public sealed class AccountDeletionWorker : PeriodicJobWorker
    {
        public AccountDeletionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<AccountDeletionWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.AccountDeletion,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.AccountDeletion.InitialDelayMinutes),
                interval: TimeSpan.FromMinutes(OperationalSettings.AccountDeletion.IntervalMinutes))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            await services.GetRequiredService<IAccountDeletionService>().PurgeDueAsync(cancellationToken);
        }
    }
}
