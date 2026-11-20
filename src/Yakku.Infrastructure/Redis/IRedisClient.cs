using System.Text.Json;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>Runs a Lua script on Redis. Shared by OTP and vote-batching stores.</summary>
    public interface IRedisScriptClient
    {
        /// <summary>EVAL: the reply as JSON (integers as numbers, bulk strings as strings, arrays as arrays).</summary>
        Task<JsonElement> EvalAsync(
            string script,
            IReadOnlyList<string> keys,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Provider-agnostic Redis access over a standard connection URL (<c>redis://</c> / <c>rediss://</c>
    /// or a StackExchange connection string). Not tied to Upstash REST.
    /// </summary>
    public interface IRedisClient : IRedisScriptClient
    {
        Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

        Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default);

        /// <summary>Atomically reads and deletes the key (GETDEL).</summary>
        Task<string?> GetDeleteAsync(string key, CancellationToken cancellationToken = default);

        /// <summary>Adds one to a counter. The first increment starts the expiry; later ones keep it.</summary>
        Task IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

        /// <summary>Sets the key only if it does not exist (SET NX EX).</summary>
        Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default);

        Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default);

        Task DeleteAsync(string key, CancellationToken cancellationToken = default);

        Task<bool> PingAsync(CancellationToken cancellationToken = default);
    }
}
