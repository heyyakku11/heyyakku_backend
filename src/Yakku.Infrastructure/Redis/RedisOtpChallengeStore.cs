using System.Text.Json;
using System.Text.Json.Serialization;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;

namespace Yakku.Infrastructure.Redis
{
    public class RedisOtpChallengeStore : IOtpChallengeStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly UpstashRedisClient _redis;

        public RedisOtpChallengeStore(UpstashRedisClient redis)
        {
            _redis = redis;
        }

        public async Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
        {
            var value = await _redis.GetAsync(Key(email), cancellationToken);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return JsonSerializer.Deserialize<OtpChallenge>(value, JsonOptions);
        }

        public async Task SetAsync(
            string email,
            OtpChallenge challenge,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(challenge, JsonOptions);
            await _redis.SetAsync(Key(email), json, ttl, cancellationToken);
        }

        public async Task<bool> ReplaceKeepingTtlAsync(
            string email,
            OtpChallenge challenge,
            CancellationToken cancellationToken = default)
        {
            var key = Key(email);
            var ttl = await _redis.GetTimeToLiveAsync(key, cancellationToken);
            if (ttl is null)
            {
                return false;
            }

            var json = JsonSerializer.Serialize(challenge, JsonOptions);
            await _redis.SetAsync(key, json, ttl.Value, cancellationToken);
            return true;
        }

        public async Task DeleteAsync(string email, CancellationToken cancellationToken = default)
        {
            await _redis.DeleteAsync(Key(email), cancellationToken);
        }

        private static string Key(string email)
        {
            return $"auth:otp:{email}";
        }
    }
}
