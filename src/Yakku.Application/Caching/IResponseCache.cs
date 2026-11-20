namespace Yakku.Application.Caching
{
    /// <summary>
    /// Cache-aside helper for GET response payloads. Implementations must fail open: a Redis outage
    /// must not fail the request (run the factory and return).
    /// </summary>
    public interface IResponseCache
    {
        /// <summary>Returns a cached value, or runs <paramref name="factory"/>, stores it, and returns it.</summary>
        Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default);

        Task RemoveAsync(string key, CancellationToken cancellationToken = default);

        Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);
    }
}
