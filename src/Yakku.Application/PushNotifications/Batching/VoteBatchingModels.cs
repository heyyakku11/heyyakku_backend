using Yakku.Domain.Enums;

namespace Yakku.Application.PushNotifications.Batching
{
    /// <summary>Identity of a card in Redis keys and in the flush schedule: <c>poll:{id}</c> or <c>check:{id}</c>.</summary>
    public static class CardKey
    {
        public static string For(ShareContentType contentType, Guid contentId) =>
            $"{contentType.ToString().ToLowerInvariant()}:{contentId:D}";

        public static bool TryParse(string? key, out ShareContentType contentType, out Guid contentId)
        {
            contentType = default;
            contentId = default;
            var parts = key?.Split(':');
            return parts is { Length: 2 }
                   && Enum.TryParse(parts[0], ignoreCase: true, out contentType)
                   && Guid.TryParse(parts[1], out contentId);
        }
    }

    public static class UnixMillis
    {
        public static long From(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        public static DateTime ToDateTime(long millis) =>
            DateTimeOffset.FromUnixTimeMilliseconds(millis).UtcDateTime;
    }

    /// <summary>The open batch of a card as Redis holds it. All times are Unix milliseconds on the Redis clock.</summary>
    public sealed record PendingBatch(
        Guid BatchId,
        int EventCount,
        long FirstPendingAt,
        long LastVoteAt,
        long NextFlushAt,
        long MaxFlushAt,
        int Generation);

    /// <summary>A vote of the same card that was already counted, sent along so the window sees the whole picture.</summary>
    public sealed record CompanionVote(Guid EventId, long At);

    /// <param name="EventId">Null reads the card's state without registering a vote (used by the hysteresis evaluation).</param>
    /// <param name="DurableMode">The card's stored mode; Redis starts its state from it when it has none.</param>
    /// <param name="Companions">Earlier votes of the card to count too (its immediate votes), so the window sees all of them.</param>
    public sealed record RegisterVoteRequest(
        string CardKey,
        Guid? EventId,
        long EventAt,
        VoteDeliveryMode DurableMode,
        IReadOnlyList<CompanionVote> Companions);

    /// <summary>
    /// What one atomic registration saw: the card's frequency after the vote was counted, its delivery mode and
    /// state version, and the open batch if there is one.
    /// </summary>
    /// <param name="Added">The vote was newly counted.</param>
    /// <param name="Stale">The vote is older than the window and was not counted.</param>
    public sealed record VoteBatchSnapshot(
        long Now,
        int Frequency,
        bool Added,
        bool Stale,
        VoteDeliveryMode Mode,
        int StateVersion,
        int LowFrequencyPeriods,
        PendingBatch? Pending)
    {
        /// <summary>The vote was already counted by an earlier attempt.</summary>
        public bool Duplicate => !Added && !Stale;
    }

    public enum EvaluationSchedule
    {
        None = 0,
        AddIfAbsent = 1,
        Set = 2,
        Remove = 3
    }

    public sealed record BatchWrite(
        Guid BatchId,
        long FirstPendingAt,
        long NextFlushAt,
        long MaxFlushAt,
        int CountDelta);

    /// <summary>
    /// One atomic compare-and-set of a card's Redis state. It only applies while the card's state version and open
    /// batch generation are still the ones the decision was made from.
    /// </summary>
    public sealed record ApplyDecisionRequest(
        string CardKey,
        int ExpectedStateVersion,
        int ExpectedGeneration,
        VoteDeliveryMode Mode,
        int LowFrequencyPeriods,
        long LastVoteAt,
        BatchWrite? Batch,
        EvaluationSchedule Evaluation,
        long NextEvaluationAt,
        long StateTtlMs);

    public sealed record ApplyDecisionResult(bool Applied, int Generation, int PendingCount);

    /// <summary>A batch handed out from the flush schedule.</summary>
    public sealed record DueBatch(
        string CardKey,
        Guid BatchId,
        int Generation,
        long NextFlushAt,
        long FirstPendingAt,
        long LastVoteAt,
        int EventCount);

    public sealed record DueWork(
        long Now,
        IReadOnlyList<DueBatch> Batches,
        IReadOnlyList<string> Evaluations,
        long QueueSize,
        long DueRemaining);

    /// <summary>
    /// Redis side of the batching: per-card rolling frequency, delivery mode, the open batch and the due-time
    /// schedules. Everything here is coordination state that can be rebuilt from the database; the durable record
    /// of every vote and batch is in PostgreSQL.
    /// </summary>
    public interface IVoteBatchingStore
    {
        Task<VoteBatchSnapshot> RegisterAsync(RegisterVoteRequest request, CancellationToken cancellationToken = default);

        Task<ApplyDecisionResult> ApplyAsync(ApplyDecisionRequest request, CancellationToken cancellationToken = default);

        /// <summary>Forgets the card's open batch if it is still <paramref name="batchId"/>, so later votes start a new one.</summary>
        Task<bool> CloseBatchAsync(string cardKey, Guid batchId, CancellationToken cancellationToken = default);

        /// <summary>Hands out due batches and due evaluations, hiding each from other workers for a lease.</summary>
        Task<DueWork> PollDueAsync(
            int batchLimit,
            int evaluationLimit,
            TimeSpan batchLease,
            TimeSpan evaluationLease,
            CancellationToken cancellationToken = default);
    }

    /// <summary>The Redis state kept changing under the decision; the vote is tried again shortly.</summary>
    public sealed class VoteBatchingConflictException : Exception
    {
        public VoteBatchingConflictException(string cardKey)
            : base($"The batching state of {cardKey} kept changing; try again.")
        {
        }
    }
}
