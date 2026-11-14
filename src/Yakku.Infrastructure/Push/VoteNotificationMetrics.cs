using System.Diagnostics.Metrics;

namespace Yakku.Infrastructure.Push
{
    /// <summary>
    /// Metrics of the vote notification pipeline, published on the <see cref="MeterName"/> meter
    /// (<c>System.Diagnostics.Metrics</c>, so OpenTelemetry, dotnet-counters or any compatible exporter can read
    /// them). Latency percentiles (p50, p95, p99) are computed by the metrics backend from the histograms. No metric
    /// carries a user, device, token or card id.
    /// </summary>
    public sealed class VoteNotificationMetrics : IDisposable
    {
        public const string MeterName = "Yakku.VoteNotifications";

        private readonly Meter _meter = new(MeterName);
        private long _queueSize;
        private long _dueWaiting;

        public VoteNotificationMetrics()
        {
            EventsClassified = _meter.CreateCounter<long>(
                "yakku.vote_notifications.events_classified",
                description: "Votes classified by the adaptive batching, by outcome (individual, batched, duplicate).");
            IndividualCreated = _meter.CreateCounter<long>(
                "yakku.vote_notifications.individual_created",
                description: "Votes that will be pushed on their own.");
            BatchesCreated = _meter.CreateCounter<long>(
                "yakku.vote_notifications.batches_created",
                description: "Batches opened.");
            BatchesFlushed = _meter.CreateCounter<long>(
                "yakku.vote_notifications.batches_flushed",
                description: "Batches that finished flushing, by result (sent, skipped, failed, retry).");
            DuplicateEvents = _meter.CreateCounter<long>(
                "yakku.vote_notifications.duplicate_events",
                description: "Votes that were already counted by an earlier attempt.");
            ModeChanges = _meter.CreateCounter<long>(
                "yakku.vote_notifications.mode_changes",
                description: "Cards that switched delivery mode, by target mode.");
            RedisFailures = _meter.CreateCounter<long>(
                "yakku.vote_notifications.redis_failures",
                description: "Redis operations of the batching that failed.");
            RedisFallbacks = _meter.CreateCounter<long>(
                "yakku.vote_notifications.redis_fallbacks",
                description: "Votes put into a database-only batch because Redis stayed unavailable.");
            ClassificationConflicts = _meter.CreateCounter<long>(
                "yakku.vote_notifications.classification_conflicts",
                description: "Votes whose Redis state kept changing and are retried.");
            PushSent = _meter.CreateCounter<long>(
                "yakku.vote_notifications.push_sent",
                description: "Pushes accepted by the push provider, by kind (individual, batch).");
            PushFailures = _meter.CreateCounter<long>(
                "yakku.vote_notifications.push_failures",
                description: "Push attempts that failed, by kind and whether the failure is final.");
            Retries = _meter.CreateCounter<long>(
                "yakku.vote_notifications.retries",
                description: "Pushes scheduled for another attempt.");
            ClaimConflicts = _meter.CreateCounter<long>(
                "yakku.vote_notifications.claim_conflicts",
                description: "Work another worker claimed first.");

            VotesPerMinute = _meter.CreateHistogram<int>(
                "yakku.vote_notifications.votes_per_minute",
                description: "A card's votes in its rolling window when one of its votes is classified.");
            BatchSize = _meter.CreateHistogram<int>(
                "yakku.vote_notifications.batch_size",
                description: "Votes in a batch when it is sent.");
            FlushDelayMs = _meter.CreateHistogram<double>(
                "yakku.vote_notifications.flush_delay",
                unit: "ms",
                description: "How long after its due time a batch was claimed.");
            DeliveryLatencyMs = _meter.CreateHistogram<double>(
                "yakku.vote_notifications.delivery_latency",
                unit: "ms",
                description: "From the vote (or the batch's first vote) to the push provider accepting the push.");
            ClassificationMs = _meter.CreateHistogram<double>(
                "yakku.vote_notifications.classification_duration",
                unit: "ms",
                description: "Time to classify one vote.");

            _meter.CreateObservableGauge(
                "yakku.vote_notifications.flush_queue_size",
                () => Interlocked.Read(ref _queueSize),
                description: "Cards with a batch waiting in the Redis flush schedule, as of the last poll.");
            _meter.CreateObservableGauge(
                "yakku.vote_notifications.flush_queue_due",
                () => Interlocked.Read(ref _dueWaiting),
                description: "Batches in the flush schedule already due after the last poll (a growing value means the dispatcher is behind).");
        }

        public Counter<long> EventsClassified { get; }
        public Counter<long> IndividualCreated { get; }
        public Counter<long> BatchesCreated { get; }
        public Counter<long> BatchesFlushed { get; }
        public Counter<long> DuplicateEvents { get; }
        public Counter<long> ModeChanges { get; }
        public Counter<long> RedisFailures { get; }
        public Counter<long> RedisFallbacks { get; }
        public Counter<long> ClassificationConflicts { get; }
        public Counter<long> PushSent { get; }
        public Counter<long> PushFailures { get; }
        public Counter<long> Retries { get; }
        public Counter<long> ClaimConflicts { get; }
        public Histogram<int> VotesPerMinute { get; }
        public Histogram<int> BatchSize { get; }
        public Histogram<double> FlushDelayMs { get; }
        public Histogram<double> DeliveryLatencyMs { get; }
        public Histogram<double> ClassificationMs { get; }

        public void SetQueue(long size, long due)
        {
            Interlocked.Exchange(ref _queueSize, size);
            Interlocked.Exchange(ref _dueWaiting, due);
        }

        public void Dispose() => _meter.Dispose();
    }
}
