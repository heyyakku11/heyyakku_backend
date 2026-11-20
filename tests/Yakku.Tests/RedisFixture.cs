using System.Text.Json;
using DotNet.Testcontainers.Builders;
using StackExchange.Redis;
using Testcontainers.Redis;
using Yakku.Infrastructure.Redis;

namespace Yakku.Tests;

/// <summary>
/// A real Redis in a container for integration tests. Skipped when Docker is unavailable. Uses the same
/// <see cref="RedisClient"/> production uses over a standard Redis TCP connection.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;
    private ConnectionMultiplexer? _connection;

    public bool IsAvailable { get; private set; }

    public string SkipReason { get; private set; } = "Docker is not available, so Redis integration tests were not run.";

    public IDatabase Database => _connection!.GetDatabase();

    public IRedisClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            _connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString() + ",allowAdmin=true");
            Client = new RedisClient(Database);
            IsAvailable = true;
        }
        catch (Exception exception) when (exception is DockerUnavailableException or TimeoutException)
        {
            IsAvailable = false;
        }
    }

    /// <summary>Starts a test from an empty Redis, as the fixture is shared by the whole collection.</summary>
    public async Task ResetAsync()
    {
        await _connection!.GetServer(_connection.GetEndPoints()[0]).FlushAllDatabasesAsync();
    }

    public async Task DisposeAsync()
    {
        _connection?.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>A Redis that is down: every call fails the way an unreachable host does.</summary>
public sealed class UnavailableScriptClient : IRedisScriptClient
{
    public int Calls { get; private set; }

    public Task<JsonElement> EvalAsync(
        string script,
        IReadOnlyList<string> keys,
        IReadOnlyList<object> arguments,
        CancellationToken ct = default)
    {
        Calls++;
        throw new HttpRequestException("Redis is unavailable.");
    }
}
