using System.Text.Json;
using System.Text.Json.Serialization;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>
    /// OTP challenges in Redis at <c>auth:otp:{phone}</c> (JSON, expires with the OTP). Every multi-step decision
    /// (cooldown, send limit, attempt counting, consuming, rollback) is one Lua script so it is atomic.
    /// </summary>
    public class RedisOtpChallengeStore : IOtpChallengeStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly IRedisScriptClient _redis;
        private readonly string _keyPrefix;
        private readonly string _sendCountPrefix;

        public RedisOtpChallengeStore(IRedisScriptClient redis)
            : this(redis, "auth:otp")
        {
        }

        public RedisOtpChallengeStore(IRedisScriptClient redis, string keyPrefix)
        {
            _redis = redis;
            _keyPrefix = keyPrefix;
            _sendCountPrefix = keyPrefix + "-sends";
        }

        public async Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
        {
            var reply = await _redis.EvalAsync(OtpChallengeScripts.Get, [Key(email)], [], cancellationToken);
            if (reply.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reply.GetString()))
            {
                return null;
            }

            return JsonSerializer.Deserialize<OtpChallenge>(reply.GetString()!, JsonOptions);
        }

        public async Task SetAsync(
            string email,
            OtpChallenge challenge,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(challenge, JsonOptions);
            await _redis.EvalAsync(OtpChallengeScripts.Set, [Key(email)], [json, Milliseconds(ttl)], cancellationToken);
        }

        public async Task<bool> ReplaceKeepingTtlAsync(
            string email,
            OtpChallenge challenge,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(challenge, JsonOptions);
            var reply = await _redis.EvalAsync(OtpChallengeScripts.ReplaceKeepingTtl, [Key(email)], [json], cancellationToken);
            return IsOne(reply);
        }

        public async Task DeleteAsync(string email, CancellationToken cancellationToken = default)
        {
            await _redis.EvalAsync(OtpChallengeScripts.Delete, [Key(email)], [], cancellationToken);
        }

        public async Task<bool> DeleteIfChallengeMatchesAsync(
            string email,
            Guid challengeId,
            CancellationToken cancellationToken = default)
        {
            return await DeleteMatchingAsync(email, challengeId, refundSend: false, cancellationToken);
        }

        public async Task<OtpIssueResult> TryIssueAsync(
            string phoneNumber,
            OtpChallenge challenge,
            OtpPolicy policy,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(challenge, JsonOptions);
            var reply = await _redis.EvalAsync(
                OtpChallengeScripts.Issue,
                [Key(phoneNumber), SendCountKey(phoneNumber)],
                [
                    json,
                    Milliseconds(policy.Ttl),
                    Milliseconds(policy.ResendCooldown),
                    policy.MaxSendsPerWindow,
                    Math.Max(1, (long)Math.Ceiling(policy.SendWindow.TotalSeconds))
                ],
                cancellationToken);

            var status = reply[0].GetInt64() switch
            {
                1 => OtpIssueStatus.Issued,
                0 => OtpIssueStatus.CooldownActive,
                2 => OtpIssueStatus.SendLimitReached,
                _ => throw new InvalidOperationException("OTP issue script returned an unknown status.")
            };

            return new OtpIssueResult(status, TimeSpan.FromMilliseconds(Math.Max(0, reply[1].GetInt64())));
        }

        public async Task<OtpVerificationResult> VerifyAndConsumeAsync(
            string phoneNumber,
            string otpHash,
            int maxAttempts,
            CancellationToken cancellationToken = default)
        {
            var reply = await _redis.EvalAsync(
                OtpChallengeScripts.VerifyAndConsume,
                [Key(phoneNumber)],
                [otpHash, maxAttempts],
                cancellationToken);

            switch (reply[0].GetInt64())
            {
                case 1:
                    var challenge = JsonSerializer.Deserialize<OtpChallenge>(reply[1].GetString()!, JsonOptions)
                        ?? throw new InvalidOperationException("Stored OTP challenge could not be read.");
                    return new OtpVerificationResult(OtpVerificationStatus.Verified, challenge);
                case 0:
                    return new OtpVerificationResult(OtpVerificationStatus.NotFound);
                case 2:
                    return new OtpVerificationResult(OtpVerificationStatus.AttemptsExceeded);
                case 3:
                    return new OtpVerificationResult(OtpVerificationStatus.Invalid);
                default:
                    throw new InvalidOperationException("OTP verify script returned an unknown status.");
            }
        }

        public async Task<bool> RevokeUndeliveredAsync(
            string phoneNumber,
            Guid challengeId,
            CancellationToken cancellationToken = default)
        {
            return await DeleteMatchingAsync(phoneNumber, challengeId, refundSend: true, cancellationToken);
        }

        private async Task<bool> DeleteMatchingAsync(
            string phoneNumber,
            Guid challengeId,
            bool refundSend,
            CancellationToken cancellationToken)
        {
            var reply = await _redis.EvalAsync(
                OtpChallengeScripts.DeleteIfChallengeMatches,
                [Key(phoneNumber), SendCountKey(phoneNumber)],
                [challengeId.ToString(), refundSend ? "1" : "0"],
                cancellationToken);
            return IsOne(reply);
        }

        private static long Milliseconds(TimeSpan duration)
        {
            return Math.Max(1, (long)Math.Ceiling(duration.TotalMilliseconds));
        }

        private static bool IsOne(JsonElement reply)
        {
            return reply.ValueKind == JsonValueKind.Number && reply.GetInt64() == 1;
        }

        private string Key(string phoneNumber)
        {
            return $"{_keyPrefix}:{phoneNumber}";
        }

        private string SendCountKey(string phoneNumber)
        {
            return $"{_sendCountPrefix}:{phoneNumber}";
        }
    }
}
