namespace Yakku.Application.Caching
{
    /// <summary>No-op cache used when Redis is not wired (unit tests). Always runs the factory.</summary>
    public sealed class NullResponseCache : IResponseCache
    {
        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default)
        {
            return factory(cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
