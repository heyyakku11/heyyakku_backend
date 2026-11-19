using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class RazorpayWebhookInboxRepository : IRazorpayWebhookInbox
    {
        private const string EventIdConstraint = "IX_webhook_events_razorpay_event_id";

        private readonly YakkuDbContext _context;

        public RazorpayWebhookInboxRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<(WebhookEvent Event, bool Created)> TryEnqueueAsync(
            string razorpayEventId,
            string eventType,
            string rawPayload,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.WebhookEvents.AsNoTracking()
                .FirstOrDefaultAsync(item => item.RazorpayEventId == razorpayEventId, cancellationToken);
            if (existing is not null)
            {
                return (existing, false);
            }

            var created = WebhookEvent.Receive(razorpayEventId, eventType, rawPayload, utcNow);
            _context.WebhookEvents.Add(created);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return (created, true);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, EventIdConstraint))
            {
                _context.Entry(created).State = EntityState.Detached;
                var raced = await _context.WebhookEvents.AsNoTracking()
                    .FirstAsync(item => item.RazorpayEventId == razorpayEventId, cancellationToken);
                return (raced, false);
            }
        }

        public async Task<WebhookEvent?> ClaimNextAsync(int maxRetries, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // Serialize claimers; SKIP LOCKED-style selection under a process-wide advisory lock is enough for one API host.
            await _context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(hashtextextended('razorpay-webhook-claim', 0))",
                cancellationToken);

            var next = await _context.WebhookEvents
                .Where(item =>
                    (item.ProcessingStatus == WebhookProcessingStatus.Received
                     || item.ProcessingStatus == WebhookProcessingStatus.Failed)
                    && item.RetryCount < maxRetries)
                .OrderBy(item => item.ReceivedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (next is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            next.MarkProcessing();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return next;
        }

        public async Task<WebhookPayload?> GetPayloadAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            var row = await _context.WebhookEvents.AsNoTracking()
                .Where(item => item.Id == eventId)
                .Select(item => new { item.Id, item.EventType, item.Payload })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? null : new WebhookPayload(row.Id, row.EventType, row.Payload);
        }

        public async Task MarkProcessedAsync(Guid eventId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var row = await _context.WebhookEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);
            if (row is null)
            {
                return;
            }

            row.MarkProcessed(utcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task MarkFailedAsync(Guid eventId, string? errorMessage, CancellationToken cancellationToken = default)
        {
            var row = await _context.WebhookEvents.FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);
            if (row is null)
            {
                return;
            }

            row.MarkFailed(errorMessage);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task ReclaimStaleProcessingAsync(
            TimeSpan staleAfter,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var cutoff = utcNow - staleAfter;
            var stale = await _context.WebhookEvents
                .Where(item =>
                    item.ProcessingStatus == WebhookProcessingStatus.Processing
                    && item.ReceivedAt < cutoff)
                .ToListAsync(cancellationToken);

            foreach (var item in stale)
            {
                item.MarkFailed("stale_processing_reclaimed");
            }

            if (stale.Count > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres
                   && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                   && postgres.ConstraintName == constraintName;
        }
    }
}
