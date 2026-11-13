using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Infrastructure.Jobs;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// A card that runs out of time never passes through the close API, so this writes its close notification.
    /// An owner close writes the same row itself; a card that already has one is skipped.
    /// </summary>
    public sealed class CardCloseNotificationWorker : PeriodicJobWorker
    {
        public CardCloseNotificationWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<CardCloseNotificationWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.CardCloseNotification,
                initialDelay: TimeSpan.FromMinutes(1),
                interval: TimeSpan.FromMinutes(2))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            var notifications = services.GetRequiredService<ICardLifecycleNotifications>();
            await notifications.NotifyExpiredAsync(DateTime.UtcNow, cancellationToken);
        }
    }
}
