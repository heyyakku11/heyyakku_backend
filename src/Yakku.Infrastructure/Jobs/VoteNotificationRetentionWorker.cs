using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Common;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Jobs
{
    /// <summary>Deletes finished vote notification outbox rows once they are past the notification retention.</summary>
    public sealed class VoteNotificationRetentionWorker : PeriodicJobWorker
    {
        public VoteNotificationRetentionWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<VoteNotificationRetentionWorker> logger)
            : base(
                scopeFactory,
                logger,
                AuditEvents.Jobs.VoteNotificationRetention,
                initialDelay: TimeSpan.FromMinutes(OperationalSettings.Retention.InitialDelayMinutes),
                interval: TimeSpan.FromHours(OperationalSettings.Retention.IntervalHours))
        {
        }

        protected override async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            var db = services.GetRequiredService<YakkuDbContext>();
            var cutoff = DateTime.UtcNow.AddDays(-OperationalSettings.Retention.NotificationDays);

            await db.VoteNotificationEvents
                .Where(e => e.CreatedAt < cutoff
                            && (e.Status == VoteNotificationStatus.Submitted
                                || e.Status == VoteNotificationStatus.Skipped
                                || e.Status == VoteNotificationStatus.Failed))
                .ExecuteDeleteAsync(cancellationToken);

            await db.VoteDigestWindows
                .Where(w => w.EndsAt < cutoff
                            && (w.Status == VoteDigestWindowStatus.Sent
                                || w.Status == VoteDigestWindowStatus.Skipped
                                || w.Status == VoteDigestWindowStatus.Failed))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
