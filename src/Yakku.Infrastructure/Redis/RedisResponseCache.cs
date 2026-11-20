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

        public async Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
            {
                return await factory(cancellationToken);
            }

            try
            {
                var cached = await _redis.GetAsync(key, cancellationToken);
                if (cached is not null)
                {
                    var hit = JsonSerializer.Deserialize<T>(cached, JsonOptions);
                    if (hit is not null)
                    {
                        return hit;
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
                var payload = JsonSerializer.Serialize(value, JsonOptions);
                await _redis.SetAsync(key, payload, ttl, cancellationToken);
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
    }
}
