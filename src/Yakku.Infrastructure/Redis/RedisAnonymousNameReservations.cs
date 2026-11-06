using Microsoft.Extensions.Logging;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>
    /// Holds candidate names in Redis. If Redis is unreachable the hold is skipped (treated as granted): the
    /// database unique index still guarantees uniqueness, so sign-up and regeneration keep working.
    /// </summary>
    public class RedisAnonymousNameReservations : IAnonymousNameReservations
    {
        private const string Prefix = "anon-name:";

        private readonly UpstashRedisClient _redis;
        private readonly ILogger<RedisAnonymousNameReservations> _logger;

        public RedisAnonymousNameReservations(UpstashRedisClient redis, ILogger<RedisAnonymousNameReservations> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<bool> TryReserveAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _redis.SetIfNotExistsAsync(Key(name), "1", holdFor, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Anonymous-name reservation unavailable; relying on the database constraint.");
                return true;
            }
        }

        public async Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
        {
            try
            {
                await _redis.SetAsync(Key(name), "1", holdFor, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(exception, "Could not refresh an anonymous-name reservation.");
            }
        }

        public async Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
        {
            try
            {
                await _redis.DeleteAsync(Key(name), cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The hold expires on its own.
                _logger.LogWarning(exception, "Could not release an anonymous-name reservation.");
            }
        }

        private static string Key(string name) => Prefix + name.ToLowerInvariant();
    }
}
