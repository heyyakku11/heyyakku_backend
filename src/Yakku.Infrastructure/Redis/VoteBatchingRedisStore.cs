using System.Globalization;
using System.Text.Json;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>
    /// <see cref="IVoteBatchingStore"/> on Redis, through small Lua scripts so each related set of commands is one
    /// atomic step. Keys (prefix <c>yakku:notification:</c>):
    /// <c>card:{card}:votes</c> ZSET of recent vote event ids scored by time, <c>card:{card}:state</c> HASH of the
    /// delivery mode, <c>card:{card}:pending</c> HASH of the open batch, <c>flush:queue</c> ZSET of cards scored by
    /// their batch's due time, and <c>evaluate:queue</c> ZSET of batched cards scored by their next evaluation.
    /// </summary>
    public sealed class VoteBatchingRedisStore : IVoteBatchingStore
    {
        public const string KeyPrefix = "yakku:notification:";
        public const string FlushQueueKey = KeyPrefix + "flush:queue";
        public const string EvaluateQueueKey = KeyPrefix + "evaluate:queue";

        private readonly IRedisScriptClient _redis;
        private readonly VoteNotificationBatchingOptions _options;

        public VoteBatchingRedisStore(IRedisScriptClient redis, VoteNotificationBatchingOptions options)
        {
            _redis = redis;
            _options = options;
        }

        public static string VotesKey(string cardKey) => $"{KeyPrefix}card:{cardKey}:votes";
        public static string StateKey(string cardKey) => $"{KeyPrefix}card:{cardKey}:state";
        public static string PendingKey(string cardKey) => $"{KeyPrefix}card:{cardKey}:pending";

        public async Task<VoteBatchSnapshot> RegisterAsync(
            RegisterVoteRequest request,
            CancellationToken cancellationToken = default)
        {
            var arguments = new List<object>
            {
                (long)_options.FrequencyWindow.TotalMilliseconds,
                request.EventId?.ToString("D") ?? string.Empty,
                request.EventAt,
                Name(request.DurableMode),
                _options.StateTtlSeconds * 1000L,
                request.Companions.Count
            };
            foreach (var companion in request.Companions)
            {
                arguments.Add(companion.EventId.ToString("D"));
                arguments.Add(companion.At);
            }

            var reply = await RunAsync(
                VoteBatchingScripts.Register,
                [VotesKey(request.CardKey), StateKey(request.CardKey), PendingKey(request.CardKey)],
                arguments,
                cancellationToken);

            var pendingReply = reply[7];
            PendingBatch? pending = pendingReply.GetArrayLength() == 7
                ? new PendingBatch(
                    Guid.Parse(Text(pendingReply[0])),
                    (int)Number(pendingReply[1]),
                    Number(pendingReply[2]),
                    Number(pendingReply[3]),
                    Number(pendingReply[4]),
                    Number(pendingReply[5]),
                    (int)Number(pendingReply[6]))
                : null;

            return new VoteBatchSnapshot(
                Number(reply[0]),
                (int)Number(reply[1]),
                Number(reply[2]) == 1,
                Number(reply[3]) == 1,
                Enum.Parse<VoteDeliveryMode>(Text(reply[4]), ignoreCase: true),
                (int)Number(reply[5]),
                (int)Number(reply[6]),
                pending);
        }

        public async Task<ApplyDecisionResult> ApplyAsync(
            ApplyDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var batch = request.Batch;
            var reply = await RunAsync(
                VoteBatchingScripts.Apply,
                [
                    StateKey(request.CardKey),
                    PendingKey(request.CardKey),
                    FlushQueueKey,
                    EvaluateQueueKey
                ],
                [
                    request.CardKey,
                    request.ExpectedStateVersion,
                    request.ExpectedGeneration,
                    Name(request.Mode),
                    request.LowFrequencyPeriods,
                    request.LastVoteAt,
                    batch is null ? 0 : 1,
                    batch?.BatchId.ToString("D") ?? string.Empty,
                    batch?.FirstPendingAt ?? 0,
                    batch?.NextFlushAt ?? 0,
                    batch?.MaxFlushAt ?? 0,
                    batch?.CountDelta ?? 0,
                    (int)request.Evaluation,
                    request.NextEvaluationAt,
                    request.StateTtlMs
                ],
                cancellationToken);

            return new ApplyDecisionResult(Number(reply[0]) == 1, (int)Number(reply[1]), (int)Number(reply[2]));
        }

        public async Task<bool> CloseBatchAsync(string cardKey, Guid batchId, CancellationToken cancellationToken = default)
        {
            var reply = await RunAsync(
                VoteBatchingScripts.Close,
                [PendingKey(cardKey), FlushQueueKey],
                [batchId.ToString("D"), cardKey],
                cancellationToken);
            return Number(reply) == 1;
        }

        public async Task<DueWork> PollDueAsync(
            int batchLimit,
            int evaluationLimit,
            TimeSpan batchLease,
            TimeSpan evaluationLease,
            CancellationToken cancellationToken = default)
        {
            var reply = await RunAsync(
                VoteBatchingScripts.Poll,
                [FlushQueueKey, EvaluateQueueKey],
                [
                    KeyPrefix,
                    batchLimit,
                    evaluationLimit,
                    (long)batchLease.TotalMilliseconds,
                    (long)evaluationLease.TotalMilliseconds
                ],
                cancellationToken);

            var batches = reply[1].EnumerateArray()
                .Select(item => new DueBatch(
                    Text(item[0]),
                    Guid.Parse(Text(item[1])),
                    (int)Number(item[2]),
                    Number(item[3]),
                    Number(item[4]),
                    Number(item[5]),
                    (int)Number(item[6])))
                .ToList();
            var evaluations = reply[2].EnumerateArray().Select(Text).ToList();
            return new DueWork(Number(reply[0]), batches, evaluations, Number(reply[3]), Number(reply[4]));
        }

        private async Task<JsonElement> RunAsync(
            string script,
            IReadOnlyList<string> keys,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _redis.EvalAsync(script, keys, arguments, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new VoteBatchingStoreException("Redis could not run the vote batching script.", exception);
            }
        }

        private static string Name(VoteDeliveryMode mode) => mode.ToString().ToUpperInvariant();

        // Redis returns integers as numbers and everything read from a hash as strings; accept both.
        private static long Number(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt64(),
            JsonValueKind.String => long.Parse(element.GetString()!, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"Unexpected Redis reply {element.ValueKind}.")
        };

        private static string Text(JsonElement element) =>
            element.ValueKind == JsonValueKind.String
                ? element.GetString()!
                : throw new InvalidOperationException($"Unexpected Redis reply {element.ValueKind}.");
    }

    /// <summary>Redis is unreachable or rejected a script. The vote is durable and is classified again later.</summary>
    public sealed class VoteBatchingStoreException : Exception
    {
        public VoteBatchingStoreException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
