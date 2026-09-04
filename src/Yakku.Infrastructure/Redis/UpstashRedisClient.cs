using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yakku.Infrastructure.Redis
{
    public sealed class UpstashRedisClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public UpstashRedisClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public static UpstashRedisClient Create(string url, string token)
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(url.TrimEnd('/') + "/")
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return new UpstashRedisClient(httpClient);
        }

        public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(["GET", key], cancellationToken);
            if (result.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                return null;
            }

            return result.GetString();
        }

        public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(ttl.TotalSeconds));
            await ExecuteAsync(["SET", key, value, "EX", seconds], cancellationToken);
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(["TTL", key], cancellationToken);
            if (result.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var seconds = result.GetInt64();
            if (seconds < 0)
            {
                return null;
            }

            return TimeSpan.FromSeconds(seconds);
        }

        public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync(["DEL", key], cancellationToken);
        }

        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            var result = await ExecuteAsync(["PING"], cancellationToken);
            return result.ValueKind == JsonValueKind.String
                && string.Equals(result.GetString(), "PONG", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<JsonElement> ExecuteAsync(object[] command, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.PostAsJsonAsync(string.Empty, command, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<UpstashResponse>(JsonOptions, cancellationToken);
            if (payload is null)
            {
                throw new InvalidOperationException("Upstash Redis returned an empty response.");
            }

            if (!string.IsNullOrWhiteSpace(payload.Error))
            {
                throw new InvalidOperationException("Upstash Redis command failed.");
            }

            return payload.Result;
        }

        private sealed class UpstashResponse
        {
            [JsonPropertyName("result")]
            public JsonElement Result { get; set; }

            [JsonPropertyName("error")]
            public string? Error { get; set; }
        }
    }
}
