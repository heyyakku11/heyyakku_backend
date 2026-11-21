namespace Yakku.Application.Caching
{
    public sealed class NullResponseCache : IResponseCache
    {
        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan softTtl,
            TimeSpan hardTtl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
