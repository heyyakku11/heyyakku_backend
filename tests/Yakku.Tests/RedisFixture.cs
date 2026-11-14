using System.Text.Json;
using DotNet.Testcontainers.Builders;
using StackExchange.Redis;
using Testcontainers.Redis;
using Yakku.Infrastructure.Redis;

namespace Yakku.Tests;

/// <summary>
/// A real Redis in a container for the batching tests. Skipped (like the PostgreSQL tests) when Docker is not
/// available. Scripts run through the same <see cref="IRedisScriptClient"/> seam production uses, so the Lua and
/// the store are tested for real; only the HTTP transport to Upstash is replaced.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;
    private ConnectionMultiplexer? _connection;

    public bool IsAvailable { get; private set; }

    public string SkipReason { get; private set; } = "Docker is not available, so Redis integration tests were not run.";

    public IDatabase Database => _connection!.GetDatabase();

    public IRedisScriptClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder("redis:7-alpine").Build();
            await _container.StartAsync();
            _connection = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString() + ",allowAdmin=true");
            Client = new StackExchangeScriptClient(Database);
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

/// <summary>
/// Runs a script with EVAL (the command the Upstash client sends) and shapes the reply like Upstash's REST API does:
/// integers as numbers, strings as strings, nil as null, arrays as arrays.
/// </summary>
public sealed class StackExchangeScriptClient : IRedisScriptClient
{
    private readonly IDatabase _database;

    public StackExchangeScriptClient(IDatabase database)
    {
        _database = database;
    }

    public async Task<JsonElement> EvalAsync(
        string script,
        IReadOnlyList<string> keys,
        IReadOnlyList<object> arguments,
        CancellationToken ct = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            script,
            keys.Select(key => (RedisKey)key).ToArray(),
            arguments.Select(argument => (RedisValue)Convert.ToString(argument, System.Globalization.CultureInfo.InvariantCulture)).ToArray());

        using var stream = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(stream))
        {
            Write(writer, result);
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement.Clone();
    }

    private static void Write(Utf8JsonWriter writer, RedisResult result)
    {
        if (result.IsNull)
        {
            writer.WriteNullValue();
            return;
        }

        switch (result.Resp2Type)
        {
            case ResultType.Integer:
                writer.WriteNumberValue((long)result);
                break;
            case ResultType.Array:
                writer.WriteStartArray();
                foreach (var item in (RedisResult[])result!)
                {
                    Write(writer, item);
                }

                writer.WriteEndArray();
                break;
            default:
                writer.WriteStringValue((string?)result);
                break;
        }
    }
}

/// <summary>A Redis that is down: every call fails the way an unreachable Upstash does.</summary>
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
