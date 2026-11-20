using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Common;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Infrastructure.Jobs;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Nudges owners of still-open cards that received no friend votes in the first hour to share again.
    /// </summary>
    public sealed class CardShareNudgeWorker : PeriodicJobWorker
    {
        public CardShareNudgeWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<CardShareNudgeWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.CardShareNudge,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.CardShareNudge.InitialDelayMinutes),
                interval: TimeSpan.FromMinutes(OperationalSettings.CardShareNudge.IntervalMinutes))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            var notifications = services.GetRequiredService<ICardLifecycleNotifications>();
            await notifications.NotifyShareNudgeAsync(DateTime.UtcNow, cancellationToken);
        }
    }
}
