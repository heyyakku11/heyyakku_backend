namespace Yakku.Infrastructure.Redis
{
    /// <summary>
    /// The Lua scripts of the vote batching. They only keep numbers atomically (count, compare-and-set, lease);
    /// every business decision is made in <c>VoteBatchingPolicy</c> and passed in. All times are Unix milliseconds
    /// from Redis <c>TIME</c>, so application instances with different clocks agree.
    /// </summary>
    internal static class VoteBatchingScripts
    {
        // Registers one vote in the card's rolling window and reads the card's state, as one atomic step.
        // KEYS: votes ZSET, state HASH, pending HASH
        // ARGV: windowMs, eventId ('' reads only), eventAt, durableMode, stateTtlMs, companionCount, then (id, at) pairs
        // Reply: now, frequency, added, stale, mode, stateVersion, lowFrequencyPeriods, pending (empty or 7 fields)
        public const string Register = """
            local t = redis.call('TIME')
            local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
            local window = tonumber(ARGV[1])
            local cutoff = now - window
            redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', '(' .. cutoff)

            local companions = tonumber(ARGV[6])
            for i = 0, companions - 1 do
              local at = math.min(tonumber(ARGV[8 + i * 2]), now)
              if at >= cutoff then
                redis.call('ZADD', KEYS[1], 'NX', at, ARGV[7 + i * 2])
              end
            end

            local added = 0
            local stale = 0
            if ARGV[2] ~= '' then
              local at = math.min(tonumber(ARGV[3]), now)
              if at >= cutoff then
                added = redis.call('ZADD', KEYS[1], 'NX', at, ARGV[2])
              else
                stale = 1
              end
            end

            local frequency = redis.call('ZCARD', KEYS[1])
            if frequency > 0 then
              redis.call('PEXPIRE', KEYS[1], window * 2)
            end

            if redis.call('EXISTS', KEYS[2]) == 0 then
              redis.call('HSET', KEYS[2], 'DeliveryMode', ARGV[4], 'LowFrequencyPeriods', 0, 'StateVersion', 0)
              redis.call('PEXPIRE', KEYS[2], ARGV[5])
            end

            local state = redis.call('HMGET', KEYS[2], 'DeliveryMode', 'StateVersion', 'LowFrequencyPeriods')
            local open = redis.call('HMGET', KEYS[3], 'BatchId', 'PendingEventCount', 'FirstPendingAt', 'LastVoteAt', 'NextFlushAt', 'MaxFlushAt', 'Generation')
            local pending = {}
            if open[1] then
              pending = open
            end
            return {now, frequency, added, stale, state[1] or ARGV[4], tonumber(state[2]) or 0, tonumber(state[3]) or 0, pending}
            """;

        // Compare-and-set of a card's state and open batch. Applies only if the state version and the batch
        // generation are still the ones the decision was made from; otherwise nothing changes.
        // KEYS: state HASH, pending HASH, flush queue ZSET, evaluate queue ZSET
        // ARGV: cardKey, expectedVersion, expectedGeneration (-1 none), mode, lowPeriods, lastVoteAt, hasBatch,
        //       batchId, firstPendingAt, nextFlushAt, maxFlushAt, countDelta, evaluation (0 none, 1 add if absent,
        //       2 set, 3 remove), nextEvaluationAt, stateTtlMs
        // Reply: applied (0 or 1), generation, pendingCount
        public const string Apply = """
            local version = tonumber(redis.call('HGET', KEYS[1], 'StateVersion') or 0)
            if version ~= tonumber(ARGV[2]) then
              return {0, 0, 0}
            end

            local generation = redis.call('HGET', KEYS[2], 'Generation')
            local expected = tonumber(ARGV[3])
            if generation then
              if tonumber(generation) ~= expected then
                return {0, 0, 0}
              end
            elseif expected >= 0 then
              return {0, 0, 0}
            end

            local t = redis.call('TIME')
            local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
            local evaluation = tonumber(ARGV[13])

            redis.call('HSET', KEYS[1], 'DeliveryMode', ARGV[4], 'LowFrequencyPeriods', ARGV[5], 'StateVersion', version + 1)
            if tonumber(ARGV[6]) > 0 then
              redis.call('HSET', KEYS[1], 'LastVoteAt', ARGV[6])
            end
            if evaluation == 2 or evaluation == 3 then
              redis.call('HSET', KEYS[1], 'LastEvaluatedAt', now)
            end
            redis.call('PEXPIRE', KEYS[1], ARGV[15])

            local newGeneration = 0
            local count = 0
            if ARGV[7] == '1' then
              if generation then
                newGeneration = tonumber(generation) + 1
                redis.call('HSET', KEYS[2], 'NextFlushAt', ARGV[10], 'LastVoteAt', ARGV[6], 'Generation', newGeneration)
                count = redis.call('HINCRBY', KEYS[2], 'PendingEventCount', ARGV[12])
              else
                newGeneration = 1
                redis.call('HSET', KEYS[2], 'BatchId', ARGV[8], 'PendingEventCount', ARGV[12], 'FirstPendingAt', ARGV[9], 'LastVoteAt', ARGV[6], 'NextFlushAt', ARGV[10], 'MaxFlushAt', ARGV[11], 'Generation', 1, 'Status', 'OPEN')
                count = tonumber(ARGV[12])
              end
              redis.call('ZADD', KEYS[3], ARGV[10], ARGV[1])
            elseif generation then
              newGeneration = tonumber(generation)
              count = tonumber(redis.call('HGET', KEYS[2], 'PendingEventCount') or 0)
            end

            if evaluation == 1 then
              redis.call('ZADD', KEYS[4], 'NX', ARGV[14], ARGV[1])
            elseif evaluation == 2 then
              redis.call('ZADD', KEYS[4], ARGV[14], ARGV[1])
            elseif evaluation == 3 then
              redis.call('ZREM', KEYS[4], ARGV[1])
            end

            return {1, newGeneration, count}
            """;

        // Forgets the open batch if it is still the given one, so the next vote starts a new batch.
        // KEYS: pending HASH, flush queue ZSET   ARGV: batchId, cardKey   Reply: 1 closed, 0 not the open batch
        public const string Close = """
            local id = redis.call('HGET', KEYS[1], 'BatchId')
            if id == ARGV[1] then
              redis.call('DEL', KEYS[1])
              redis.call('ZREM', KEYS[2], ARGV[2])
              return 1
            end
            return 0
            """;

        // Hands out the due batches and evaluations. A handed-out entry is moved forward by a lease so other workers
        // do not see it; if the worker dies it becomes visible again. The batch's own deadline is checked here, so a
        // queue entry that is older than a later reschedule can never release a batch early.
        // KEYS: flush queue ZSET, evaluate queue ZSET
        // ARGV: keyPrefix, batchLimit, evaluationLimit, batchLeaseMs, evaluationLeaseMs
        // Reply: now, batches (cardKey, batchId, generation, nextFlushAt, firstPendingAt, lastVoteAt, count),
        //        evaluations (cardKey), queueSize, dueRemaining
        public const string Poll = """
            local t = redis.call('TIME')
            local now = tonumber(t[1]) * 1000 + math.floor(tonumber(t[2]) / 1000)
            local prefix = ARGV[1]

            local batches = {}
            local due = redis.call('ZRANGEBYSCORE', KEYS[1], '-inf', now, 'LIMIT', 0, tonumber(ARGV[2]))
            for _, card in ipairs(due) do
              local open = redis.call('HMGET', prefix .. 'card:' .. card .. ':pending', 'BatchId', 'Generation', 'NextFlushAt', 'FirstPendingAt', 'LastVoteAt', 'PendingEventCount')
              if not open[1] then
                redis.call('ZREM', KEYS[1], card)
              else
                local nextFlushAt = tonumber(open[3])
                if nextFlushAt > now then
                  redis.call('ZADD', KEYS[1], nextFlushAt, card)
                else
                  redis.call('ZADD', KEYS[1], now + tonumber(ARGV[4]), card)
                  table.insert(batches, {card, open[1], tonumber(open[2]), nextFlushAt, tonumber(open[4]), tonumber(open[5]), tonumber(open[6])})
                end
              end
            end

            local evaluations = {}
            local dueEvaluations = redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', now, 'LIMIT', 0, tonumber(ARGV[3]))
            for _, card in ipairs(dueEvaluations) do
              redis.call('ZADD', KEYS[2], now + tonumber(ARGV[5]), card)
              table.insert(evaluations, card)
            end

            return {now, batches, evaluations, redis.call('ZCARD', KEYS[1]), redis.call('ZCOUNT', KEYS[1], '-inf', now)}
            """;
    }
}
