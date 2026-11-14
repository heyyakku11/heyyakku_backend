using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Redis;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// The hysteresis of the delivery mode. A batched card is re-evaluated once per evaluation period; after the
    /// configured number of consecutive low-frequency periods it goes back to individual mode, so a single quiet
    /// moment never flips it. Each card is evaluated from its own window only.
    /// </summary>
    public sealed class CardModeEvaluator
    {
        private readonly YakkuDbContext _db;
        private readonly VoteBatchingEngine _engine;
        private readonly VoteNotificationMetrics _metrics;
        private readonly TimeProvider _time;
        private readonly ILogger<CardModeEvaluator> _logger;

        public CardModeEvaluator(
            YakkuDbContext db,
            VoteBatchingEngine engine,
            VoteNotificationMetrics metrics,
            TimeProvider time,
            ILogger<CardModeEvaluator> logger)
        {
            _db = db;
            _engine = engine;
            _metrics = metrics;
            _time = time;
            _logger = logger;
        }

        public async Task EvaluateAsync(string cardKey, CancellationToken cancellationToken)
        {
            if (!CardKey.TryParse(cardKey, out var contentType, out var contentId))
            {
                _logger.LogWarning("Ignoring an evaluation entry with an unreadable card key.");
                return;
            }

            var card = await _db.VoteNotificationCards
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.ContentType == contentType && c.ContentId == contentId, cancellationToken);
            if (card is null)
            {
                return;
            }

            EvaluationOutcome outcome;
            try
            {
                outcome = await _engine.EvaluateAsync(cardKey, card.DeliveryMode, cancellationToken);
            }
            catch (Exception exception) when (exception is VoteBatchingStoreException or VoteBatchingConflictException)
            {
                // The entry stays in the schedule and comes back after its lease.
                _metrics.RedisFailures.Add(exception is VoteBatchingStoreException ? 1 : 0);
                _logger.LogWarning(exception, "Card mode evaluation failed; it will be retried.");
                return;
            }

            if (outcome.Mode != card.DeliveryMode)
            {
                var now = _time.GetUtcNow().UtcDateTime;
                await _db.VoteNotificationCards
                    .Where(c => c.ContentType == contentType && c.ContentId == contentId)
                    .ExecuteUpdateAsync(
                        set => set
                            .SetProperty(c => c.DeliveryMode, outcome.Mode)
                            .SetProperty(c => c.UpdatedAt, now),
                        cancellationToken);
            }

            if (outcome.ModeChanged)
            {
                _metrics.ModeChanges.Add(1, new KeyValuePair<string, object?>("mode", outcome.Mode.ToString().ToLowerInvariant()));
            }
        }
    }
}
