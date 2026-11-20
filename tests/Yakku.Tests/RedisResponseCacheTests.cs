using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Configuration;
using Yakku.Infrastructure.Redis;

namespace Yakku.Tests;

public sealed class RedisResponseCacheTests
{
    [Fact]
    public async Task Miss_RunsFactory_AndStoresValue()
    {
        var redis = new MemoryRedis();
        var cache = NewCache(redis);
        var calls = 0;

        var first = await cache.GetOrSetAsync(
            "yakku:resp:v1:t",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                calls++;
                return Task.FromResult(new SampleDto { Name = "a" });
            });

        Assert.Equal("a", first.Name);
        Assert.Equal(1, calls);
        Assert.NotNull(await redis.GetAsync("yakku:resp:v1:t"));
    }

    [Fact]
    public async Task Hit_SkipsFactory()
    {
        var redis = new MemoryRedis();
        var cache = NewCache(redis);
        var calls = 0;

        await cache.GetOrSetAsync(
            "k",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                calls++;
                return Task.FromResult(new SampleDto { Name = "once" });
            });

        var second = await cache.GetOrSetAsync(
            "k",
            TimeSpan.FromMinutes(1),
            _ =>
            {
                calls++;
                return Task.FromResult(new SampleDto { Name = "again" });
            });

        Assert.Equal("once", second.Name);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Remove_ForcesNextMiss()
    {
        var redis = new MemoryRedis();
        var cache = NewCache(redis);
        var calls = 0;

        await cache.GetOrSetAsync("k", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new SampleDto { Name = "v1" });
        });
        await cache.RemoveAsync("k");

        var after = await cache.GetOrSetAsync("k", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new SampleDto { Name = "v2" });
        });

        Assert.Equal("v2", after.Name);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RedisGetFailure_FallsThroughToFactory()
    {
        var cache = NewCache(new ThrowingRedis());
        var value = await cache.GetOrSetAsync(
            "k",
            TimeSpan.FromMinutes(1),
            _ => Task.FromResult(new SampleDto { Name = "fresh" }));

        Assert.Equal("fresh", value.Name);
    }

    [Fact]
    public async Task Disabled_NeverTouchesRedis()
    {
        var redis = new MemoryRedis();
        var cache = NewCache(redis, enabled: false);
        var calls = 0;

        await cache.GetOrSetAsync("k", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new SampleDto { Name = "a" });
        });
        await cache.GetOrSetAsync("k", TimeSpan.FromMinutes(1), _ =>
        {
            calls++;
            return Task.FromResult(new SampleDto { Name = "b" });
        });

        Assert.Equal(2, calls);
        Assert.Empty(redis.Store);
    }

    [Collection(PostgresCollection.Name)]
    public sealed class AgainstRealRedis
    {
        private readonly RedisFixture _redis;

        public AgainstRealRedis(RedisFixture redis) => _redis = redis;

        [SkippableFact]
        public async Task SetUsesTtl_AndRemoveDeletes()
        {
            Skip.IfNot(_redis.IsAvailable, _redis.SkipReason);
            await _redis.ResetAsync();

            var cache = NewCache(_redis.Client);
            await cache.GetOrSetAsync(
                "yakku:resp:v1:ttl",
                TimeSpan.FromSeconds(30),
                _ => Task.FromResult(new SampleDto { Name = "x" }));

            var ttl = await _redis.Client.GetTimeToLiveAsync("yakku:resp:v1:ttl");
            Assert.NotNull(ttl);
            Assert.True(ttl > TimeSpan.Zero);
            Assert.True(ttl <= TimeSpan.FromSeconds(30));

            await cache.RemoveAsync("yakku:resp:v1:ttl");
            Assert.Null(await _redis.Client.GetAsync("yakku:resp:v1:ttl"));
        }
    }

    private static RedisResponseCache NewCache(IRedisClient redis, bool enabled = true) =>
        new(
            redis,
            Options.Create(new ResponseCacheOptions { Enabled = enabled }),
            NullLogger<RedisResponseCache>.Instance);

    private sealed class SampleDto
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class MemoryRedis : IRedisClient
    {
        public Dictionary<string, string> Store { get; } = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.TryGetValue(key, out var value) ? value : null);

        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            Store[key] = value;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            Store.Remove(key);
            return Task.CompletedTask;
        }

        public Task<string?> GetDeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<TimeSpan?>(TimeSpan.FromMinutes(1));

        public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<System.Text.Json.JsonElement> EvalAsync(
            string script,
            IReadOnlyList<string> keys,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingRedis : IRedisClient
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task<string?> GetDeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task<bool> PingAsync(CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");

        public Task<System.Text.Json.JsonElement> EvalAsync(
            string script,
            IReadOnlyList<string> keys,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("Redis down");
    }
}
