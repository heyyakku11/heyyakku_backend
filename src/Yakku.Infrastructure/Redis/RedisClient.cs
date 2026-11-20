using System.Globalization;
using System.Text.Json;
using StackExchange.Redis;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>StackExchange.Redis implementation of <see cref="IRedisClient"/>.</summary>
    public sealed class RedisClient : IRedisClient
    {
        private readonly IDatabase _database;

        public RedisClient(IConnectionMultiplexer connection)
        {
            _database = connection.GetDatabase();
        }

        public RedisClient(IDatabase database)
        {
            _database = database;
        }

        public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            var value = await _database.StringGetAsync(key).WaitAsync(cancellationToken);
            return value.IsNullOrEmpty ? null : value.ToString();
        }

        public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            var expiry = ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : ttl;
            await _database.StringSetAsync(key, value, expiry).WaitAsync(cancellationToken);
        }

        public async Task<string?> GetDeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            // GETDEL is Redis 6.2+; fall back to GET + DEL when unavailable.
            try
            {
                var value = await _database.StringGetDeleteAsync(key).WaitAsync(cancellationToken);
                return value.IsNullOrEmpty ? null : value.ToString();
            }
            catch (RedisServerException)
            {
                var value = await _database.StringGetAsync(key).WaitAsync(cancellationToken);
                if (value.IsNullOrEmpty)
                {
                    return null;
                }

                await _database.KeyDeleteAsync(key).WaitAsync(cancellationToken);
                return value.ToString();
            }
        }

        public async Task IncrementAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            var count = await _database.StringIncrementAsync(key).WaitAsync(cancellationToken);
            if (count == 1)
            {
                var expiry = ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : ttl;
                await _database.KeyExpireAsync(key, expiry).WaitAsync(cancellationToken);
            }
        }

        public async Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            var expiry = ttl <= TimeSpan.Zero ? TimeSpan.FromSeconds(1) : ttl;
            return await _database.StringSetAsync(key, value, expiry, When.NotExists).WaitAsync(cancellationToken);
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
        {
            var ttl = await _database.KeyTimeToLiveAsync(key).WaitAsync(cancellationToken);
            return ttl;
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            await _database.KeyDeleteAsync(key).WaitAsync(cancellationToken);
        }

        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            var latency = await _database.PingAsync().WaitAsync(cancellationToken);
            return latency >= TimeSpan.Zero;
        }

        public async Task<JsonElement> EvalAsync(
            string script,
            IReadOnlyList<string> keys,
            IReadOnlyList<object> arguments,
            CancellationToken cancellationToken = default)
        {
            var result = await _database.ScriptEvaluateAsync(
                script,
                keys.Select(key => (RedisKey)key).ToArray(),
                arguments.Select(argument => (RedisValue)Convert.ToString(argument, CultureInfo.InvariantCulture))
                    .ToArray()).WaitAsync(cancellationToken);

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
}
