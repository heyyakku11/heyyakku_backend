namespace Yakku.Infrastructure.Redis
{
    /// <summary>
    /// The Lua scripts behind the OTP challenge store. Each one is a single atomic step on Redis, so concurrent
    /// requests (several sends, several guesses, a late rollback) can never interleave between a read and a write.
    /// Challenges are the JSON the store writes (camelCase), read here with cjson. Time comes from Redis (PTTL),
    /// so application instances with different clocks agree.
    /// </summary>
    internal static class OtpChallengeScripts
    {
        public const string Get = "return redis.call('GET', KEYS[1])";

        // ARGV: json, ttlMs
        public const string Set = """
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
            return 1
            """;

        // Overwrites the challenge but keeps the time it has left. 0 when it is gone or has no expiry.
        // ARGV: json
        public const string ReplaceKeepingTtl = """
            local pttl = redis.call('PTTL', KEYS[1])
            if pttl <= 0 then
              return 0
            end
            redis.call('SET', KEYS[1], ARGV[1], 'PX', pttl)
            return 1
            """;

        public const string Delete = "return redis.call('DEL', KEYS[1])";

        // Deletes the challenge only when it still has this id; optionally gives back one send.
        // KEYS: challenge, send counter. ARGV: challengeId, refundSend ('1' or '0')
        public const string DeleteIfChallengeMatches = """
            local raw = redis.call('GET', KEYS[1])
            if not raw then
              return 0
            end
            local ok, challenge = pcall(cjson.decode, raw)
            if not ok or type(challenge) ~= 'table' or challenge.challengeId ~= ARGV[1] then
              return 0
            end
            redis.call('DEL', KEYS[1])
            if ARGV[2] == '1' then
              local sends = tonumber(redis.call('GET', KEYS[2]) or '0')
              if sends > 1 then
                redis.call('DECR', KEYS[2])
              elseif sends == 1 then
                redis.call('DEL', KEYS[2])
              end
            end
            return 1
            """;

        // Cooldown, send limit and store, as one step. A challenge's age is the TTL it has used up
        // (ttl - PTTL), which needs no timestamp in the JSON and no clock on the application servers.
        // KEYS: challenge, send counter. ARGV: json, ttlMs, cooldownMs, maxSends, windowSeconds
        // Reply: {status, retryAfterMs}; status 1 issued, 0 cooldown active, 2 send limit reached
        public const string Issue = """
            local ttl = tonumber(ARGV[2])
            local cooldown = tonumber(ARGV[3])
            local pttl = redis.call('PTTL', KEYS[1])
            if pttl > 0 and pttl > (ttl - cooldown) then
              return {0, pttl - (ttl - cooldown)}
            end

            local sends = tonumber(redis.call('GET', KEYS[2]) or '0')
            if sends >= tonumber(ARGV[4]) then
              local wait = redis.call('PTTL', KEYS[2])
              if wait < 0 then
                wait = tonumber(ARGV[5]) * 1000
              end
              return {2, wait}
            end

            sends = redis.call('INCR', KEYS[2])
            if sends == 1 or redis.call('PTTL', KEYS[2]) < 0 then
              redis.call('EXPIRE', KEYS[2], ARGV[5])
            end
            redis.call('SET', KEYS[1], ARGV[1], 'PX', ttl)
            return {1, 0}
            """;

        // Compare, count and consume as one step. The time left on the challenge is never extended.
        // KEYS: challenge. ARGV: otpHash, maxAttempts
        // Reply: {status, challengeJson}; status 1 verified (and consumed), 0 not found, 3 wrong, 2 attempts exceeded
        public const string VerifyAndConsume = """
            local raw = redis.call('GET', KEYS[1])
            if not raw then
              return {0, ''}
            end
            local ok, challenge = pcall(cjson.decode, raw)
            if not ok or type(challenge) ~= 'table' then
              redis.call('DEL', KEYS[1])
              return {0, ''}
            end

            local max = tonumber(ARGV[2])
            local attempts = tonumber(challenge.attemptCount) or 0
            if attempts >= max then
              return {2, ''}
            end

            if challenge.otpHash == ARGV[1] then
              redis.call('DEL', KEYS[1])
              return {1, raw}
            end

            local pttl = redis.call('PTTL', KEYS[1])
            if pttl <= 0 then
              redis.call('DEL', KEYS[1])
              return {0, ''}
            end
            attempts = attempts + 1
            challenge.attemptCount = attempts
            redis.call('SET', KEYS[1], cjson.encode(challenge), 'PX', pttl)
            if attempts >= max then
              return {2, ''}
            end
            return {3, ''}
            """;
    }
}
