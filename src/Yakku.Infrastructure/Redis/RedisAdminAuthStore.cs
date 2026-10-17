using System.Text.Json;
using Yakku.Application.AdminAuth.Interfaces;

namespace Yakku.Infrastructure.Redis
{
    public sealed class RedisAdminAuthStore : IAdminAuthStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly UpstashRedisClient _redis;

        public RedisAdminAuthStore(UpstashRedisClient redis)
        {
            _redis = redis;
        }

        public async Task<PendingAdminRegistration?> GetPendingAsync(string email, CancellationToken cancellationToken = default)
        {
            var json = await _redis.GetAsync(PendingKey(email), cancellationToken);
            return json is null ? null : JsonSerializer.Deserialize<PendingAdminRegistration>(json, JsonOptions);
        }

        public Task SetPendingAsync(
            string email,
            PendingAdminRegistration pending,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            return _redis.SetAsync(PendingKey(email), JsonSerializer.Serialize(pending, JsonOptions), ttl, cancellationToken);
        }

        public async Task<bool> ReplacePendingKeepingTtlAsync(
            string email,
            PendingAdminRegistration pending,
            CancellationToken cancellationToken = default)
        {
            var key = PendingKey(email);
            var ttl = await _redis.GetTimeToLiveAsync(key, cancellationToken);
            if (ttl is null)
            {
                return false;
            }

            await _redis.SetAsync(key, JsonSerializer.Serialize(pending, JsonOptions), ttl.Value, cancellationToken);
            return true;
        }

        public Task DeletePendingAsync(string email, CancellationToken cancellationToken = default)
        {
            return _redis.DeleteAsync(PendingKey(email), cancellationToken);
        }

        public Task SetVerificationTokenAsync(
            string tokenHash,
            string email,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            return _redis.SetAsync(VerifyKey(tokenHash), email, ttl, cancellationToken);
        }

        public Task<string?> ConsumeVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return _redis.GetDeleteAsync(VerifyKey(tokenHash), cancellationToken);
        }

        public async Task SetResetTokenAsync(
            Guid adminId,
            string tokenHash,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            // One live reset token per admin: a new request invalidates the previous link.
            var pointerKey = ResetPointerKey(adminId);
            var previous = await _redis.GetAsync(pointerKey, cancellationToken);
            if (previous is not null)
            {
                await _redis.DeleteAsync(ResetKey(previous), cancellationToken);
            }

            await _redis.SetAsync(ResetKey(tokenHash), adminId.ToString("D"), ttl, cancellationToken);
            await _redis.SetAsync(pointerKey, tokenHash, ttl, cancellationToken);
        }

        public async Task<Guid?> ConsumeResetTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            var value = await _redis.GetDeleteAsync(ResetKey(tokenHash), cancellationToken);
            if (value is null || !Guid.TryParse(value, out var adminId))
            {
                return null;
            }

            await _redis.DeleteAsync(ResetPointerKey(adminId), cancellationToken);
            return adminId;
        }

        public Task<bool> TryStartForgotPasswordCooldownAsync(
            string email,
            TimeSpan cooldown,
            CancellationToken cancellationToken = default)
        {
            return _redis.SetIfNotExistsAsync($"admin:forgot-cooldown:{email}", "1", cooldown, cancellationToken);
        }

        private static string PendingKey(string email) => $"admin:reg:{email}";

        private static string VerifyKey(string tokenHash) => $"admin:verify:{tokenHash}";

        private static string ResetKey(string tokenHash) => $"admin:reset:{tokenHash}";

        private static string ResetPointerKey(Guid adminId) => $"admin:reset-current:{adminId:D}";
    }
}
