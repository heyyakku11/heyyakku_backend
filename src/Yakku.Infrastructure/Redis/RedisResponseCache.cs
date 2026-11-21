using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Configuration;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>Redis-backed <see cref="IResponseCache"/>. Fail-open on Redis errors.</summary>
    public sealed class RedisResponseCache : IResponseCache
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IRedisClient _redis;
        private readonly ResponseCacheOptions _options;
        private readonly ILogger<RedisResponseCache> _logger;

        public RedisResponseCache(
            IRedisClient redis,
            IOptions<ResponseCacheOptions> options,
            ILogger<RedisResponseCache> logger)
        {
            _redis = redis;
            _options = options.Value;
            _logger = logger;
        }

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) =>
            GetOrSetAsync(key, softTtl: ttl, hardTtl: ttl, factory, cancellationToken);

        public async Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan softTtl,
            TimeSpan hardTtl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return await factory(cancellationToken);
            }

            var softSeconds = Math.Max(1, (int)softTtl.TotalSeconds);
            var hardSeconds = Math.Max(softSeconds, (int)hardTtl.TotalSeconds);
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            try
            {
                var cached = await _redis.GetAsync(key, cancellationToken);
                if (cached is not null)
                {
                    var envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(cached, JsonOptions);
                    if (envelope is not null && envelope.HasPayload)
                    {
                        var age = nowUnix - envelope.CachedAtUnix;
                        if (age < softSeconds)
                        {
                            return envelope.Payload!;
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Response cache GET failed for {Key}; loading from source.", key);
            }

            var value = await factory(cancellationToken);

            try
            {
                var envelope = new CacheEnvelope<T>
                {
                    Payload = value,
                    CachedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    HasPayload = true
                };
                var payload = JsonSerializer.Serialize(envelope, JsonOptions);
                await _redis.SetAsync(key, payload, TimeSpan.FromSeconds(hardSeconds), cancellationToken);
                _logger.LogDebug(
                    "Response cache SET {Key} ({Bytes} bytes, soft {Soft}s hard {Hard}s).",
                    key,
                    payload.Length,
                    softSeconds,
                    hardSeconds);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Response cache SET failed for {Key}; returning fresh value.", key);
            }

            return value;
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return;
            }

            try
            {
                await _redis.DeleteAsync(key, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Response cache DELETE failed for {Key}.", key);
            }
        }

        public async Task RemoveManyAsync(
            IEnumerable<string> keys,
            CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
            {
                await RemoveAsync(key, cancellationToken);
            }
        }

        private sealed class CacheEnvelope<T>
        {
            public T Payload { get; set; } = default!;
            public long CachedAtUnix { get; set; }
            public bool HasPayload { get; set; } = true;
        }
    }
}
