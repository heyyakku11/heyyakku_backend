using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yakku.Application.AskBack;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Jobs
{
    /// <summary>
    /// Settles deliveries nobody acknowledged: Sent past the timeout becomes TimedOut (the message may still have
    /// arrived, so a late acknowledgment can still move it to Delivered), and Pending past the timeout becomes Failed
    /// (the send was never attempted, e.g. the process stopped after the commit). Each row moves with a conditional
    /// update, so an acknowledgment landing meanwhile wins and several instances can sweep at once.
    /// </summary>
    public sealed class AskBackDeliveryTimeoutSweeper
    {
        /// <summary>Batches per run, so one run cannot hold the job for long after an outage.</summary>
        private const int MaxBatchesPerRun = 10;

        private readonly YakkuDbContext _db;
        private readonly AskBackDeliveryOptions _options;
        private readonly TimeProvider _time;
        private readonly ILogger<AskBackDeliveryTimeoutSweeper> _logger;

        public AskBackDeliveryTimeoutSweeper(
            YakkuDbContext db,
            IOptions<AskBackDeliveryOptions> options,
            TimeProvider time,
            ILogger<AskBackDeliveryTimeoutSweeper> logger)
        {
            _db = db;
            _options = options.Value;
            _time = time;
            _logger = logger;
        }

        /// <summary>Returns how many rows changed.</summary>
        public async Task<int> SweepAsync(CancellationToken cancellationToken)
        {
            var changed = 0;
            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                var now = _time.GetUtcNow().UtcDateTime;
                var cutoff = now - _options.AckTimeout;
                var candidates = await _db.AskBackDeliveries
                    .AsNoTracking()
                    .Where(d => (d.Status == AskBackDeliveryStatus.Sent && d.SentAt < cutoff)
                                || (d.Status == AskBackDeliveryStatus.Pending && d.CreatedAt < cutoff))
                    .OrderBy(d => d.CreatedAt)
                    .Select(d => new
                    {
                        d.Id,
                        d.MessageId,
                        d.RecipientUserId,
                        d.Status,
                        d.SentAt,
                        d.Message.ConversationId
                    })
                    .Take(_options.SweepBatchSize)
                    .ToListAsync(cancellationToken);

                foreach (var row in candidates)
                {
                    var next = row.Status == AskBackDeliveryStatus.Sent
                        ? AskBackDeliveryStatus.TimedOut
                        : AskBackDeliveryStatus.Failed;
                    var updated = await _db.AskBackDeliveries
                        .Where(d => d.Id == row.Id && d.Status == row.Status)
                        .ExecuteUpdateAsync(
                            set => set
                                .SetProperty(d => d.Status, next)
                                .SetProperty(d => d.UpdatedAt, now),
                            cancellationToken);
                    if (updated == 0)
                    {
                        // Acknowledged (or swept by another instance) since it was read.
                        continue;
                    }

                    changed++;
                    if (next == AskBackDeliveryStatus.TimedOut)
                    {
                        _logger.LogWarning(
                            "Ask Back acknowledgment timeout detected for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId}: status {DeliveryStatus}, sent at {SentAt}, detected at {Timestamp}.",
                            row.MessageId, row.ConversationId, row.RecipientUserId, next, row.SentAt, now);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Ask Back delivery attempt never made for message {MessageId} in conversation {ConversationId} to recipient {RecipientUserId}: status {DeliveryStatus} at {Timestamp}.",
                            row.MessageId, row.ConversationId, row.RecipientUserId, next, now);
                    }
                }

                if (candidates.Count < _options.SweepBatchSize)
                {
                    break;
                }
            }

            return changed;
        }
    }
}
