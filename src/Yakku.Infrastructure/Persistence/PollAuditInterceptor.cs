using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence
{
    public sealed class PollAuditInterceptor : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            StampUpdatedAt(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            StampUpdatedAt(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void StampUpdatedAt(DbContext? context)
        {
            if (context is null)
            {
                return;
            }

            var utcNow = DateTime.UtcNow;
            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Modified)
                {
                    continue;
                }

                if (entry.Entity is Poll or PollOption or PollVote or MediaUploadSession)
                {
                    entry.Property("UpdatedAt").CurrentValue = utcNow;
                }
            }
        }
    }
}
