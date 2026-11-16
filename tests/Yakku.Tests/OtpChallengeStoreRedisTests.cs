using System.Text.Json;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Models;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Redis;

namespace Yakku.Tests;

/// <summary>
/// The OTP challenge store against a real Redis: the Lua scripts, their atomicity under concurrency, expiry by
/// TTL, brute-force limits and rollback. Skipped when Docker is not available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpChallengeStoreRedisTests
{
    private const string Phone = "+919876543210";
    private const string ChallengeKey = "auth:otp:" + Phone;
    private const string SendCountKey = "auth:otp-sends:" + Phone;

    private readonly RedisFixture _redis;

    public OtpChallengeStoreRedisTests(RedisFixture redis)
    {
        _redis = redis;
    }

    private async Task<RedisOtpChallengeStore> StartAsync()
    {
        Skip.IfNot(_redis.IsAvailable, _redis.SkipReason);
        await _redis.ResetAsync();
        return new RedisOtpChallengeStore(_redis.Client);
    }

    private static OtpChallenge Challenge(string otp = "111111", Guid? id = null, string? name = null) => new()
    {
        ChallengeId = id ?? Guid.NewGuid(),
        OtpHash = OtpHasher.Hash(Phone, otp),
        Purpose = OtpPurpose.Registration,
        AnonymousName = name,
        AttemptCount = 0,
        CreatedAt = DateTime.UtcNow
    };

    private static string Hash(string otp) => OtpHasher.Hash(Phone, otp);

    private static readonly OtpPolicy Policy = OtpPolicy.Default;

    // Short cooldown so tests that need a second issue do not wait a minute.
    private static readonly OtpPolicy QuickCooldown = new() { ResendCooldown = TimeSpan.FromMilliseconds(150) };

    private async Task<JsonElement> StoredAsync() =>
        JsonDocument.Parse((string)(await _redis.Database.StringGetAsync(ChallengeKey))!).RootElement.Clone();

    // ---- issue: storage, expiry, cooldown, limits ----------------------------------------------------------

    [SkippableFact]
    public async Task Issue_StoresTheChallenge_WithTheConfiguredTtl_AndNoPlainOtp()
    {
        var store = await StartAsync();
        var challenge = Challenge(otp: "482913");

        var result = await store.TryIssueAsync(Phone, challenge, Policy);

        Assert.Equal(OtpIssueStatus.Issued, result.Status);
        var ttl = await _redis.Database.KeyTimeToLiveAsync(ChallengeKey);
        Assert.NotNull(ttl);
        Assert.InRange(ttl!.Value.TotalSeconds, 298, 300);
        var raw = (string)(await _redis.Database.StringGetAsync(ChallengeKey))!;
        Assert.DoesNotContain("482913", raw);
        Assert.Contains(challenge.ChallengeId.ToString(), raw);
    }

    [SkippableFact]
    public async Task Issue_StoresTheOtpForAtMostFiveMinutes()
    {
        var store = await StartAsync();

        await store.TryIssueAsync(Phone, Challenge(), OtpPolicy.Default);

        var ttl = await _redis.Database.KeyTimeToLiveAsync(ChallengeKey);
        Assert.InRange(ttl!.Value.TotalSeconds, 298, 300);
    }

    [SkippableFact]
    public async Task ResendCooldown_RefusesASecondIssue_AndKeepsTheFirstOtp()
    {
        var store = await StartAsync();
        var first = Challenge("111111");
        await store.TryIssueAsync(Phone, first, Policy);

        var second = await store.TryIssueAsync(Phone, Challenge("222222"), Policy);

        Assert.Equal(OtpIssueStatus.CooldownActive, second.Status);
        Assert.InRange(second.RetryAfter.TotalSeconds, 1, 60);
        Assert.Equal(first.ChallengeId.ToString(), (await StoredAsync()).GetProperty("challengeId").GetString());
        Assert.Equal("1", (string?)await _redis.Database.StringGetAsync(SendCountKey)); // the refused request is not counted
    }

    [SkippableFact]
    public async Task AfterTheCooldown_ANewOtp_ReplacesTheOldOne()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("111111"), QuickCooldown);
        await Task.Delay(400);
        var next = Challenge("222222");

        var result = await store.TryIssueAsync(Phone, next, QuickCooldown);

        Assert.Equal(OtpIssueStatus.Issued, result.Status);
        Assert.Equal(OtpVerificationStatus.Invalid, (await store.VerifyAndConsumeAsync(Phone, Hash("111111"), 5)).Status);
        Assert.Equal(OtpVerificationStatus.Verified, (await store.VerifyAndConsumeAsync(Phone, Hash("222222"), 5)).Status);
    }

    [SkippableFact]
    public async Task TheHourlySendLimit_StopsFurtherOtps_ForThatNumberOnly()
    {
        var store = await StartAsync();
        var policy = new OtpPolicy { ResendCooldown = TimeSpan.FromMilliseconds(1), MaxSendsPerWindow = 2 };

        Assert.Equal(OtpIssueStatus.Issued, (await store.TryIssueAsync(Phone, Challenge(), policy)).Status);
        await Task.Delay(30);
        Assert.Equal(OtpIssueStatus.Issued, (await store.TryIssueAsync(Phone, Challenge(), policy)).Status);
        await Task.Delay(30);
        var blocked = await store.TryIssueAsync(Phone, Challenge(), policy);
        var otherNumber = await store.TryIssueAsync("+919123456789", Challenge(), policy);

        Assert.Equal(OtpIssueStatus.SendLimitReached, blocked.Status);
        Assert.True(blocked.RetryAfter > TimeSpan.Zero);
        Assert.Equal(OtpIssueStatus.Issued, otherNumber.Status);
        var window = await _redis.Database.KeyTimeToLiveAsync(SendCountKey);
        Assert.InRange(window!.Value.TotalMinutes, 58, 60);
    }

    [SkippableFact]
    public async Task ConcurrentSends_ToOneNumber_IssueExactlyOneOtp()
    {
        var store = await StartAsync();
        var attempts = Enumerable.Range(0, 30).Select(_ => Challenge()).ToList();

        var results = await Task.WhenAll(attempts.Select(c => Task.Run(() => store.TryIssueAsync(Phone, c, Policy))));

        Assert.Equal(1, results.Count(r => r.Status == OtpIssueStatus.Issued));
        Assert.Equal(29, results.Count(r => r.Status == OtpIssueStatus.CooldownActive));
        var winner = attempts[Array.FindIndex(results, r => r.Status == OtpIssueStatus.Issued)];
        Assert.Equal(winner.ChallengeId.ToString(), (await StoredAsync()).GetProperty("challengeId").GetString());
        Assert.Equal("1", (string?)await _redis.Database.StringGetAsync(SendCountKey));
    }

    // ---- verify: expiry, failures, consumption, brute force ------------------------------------------------

    [SkippableFact]
    public async Task ACorrectOtp_Verifies_AndReturnsTheChallenge()
    {
        var store = await StartAsync();
        var challenge = Challenge("482913", name: "Quiet Fox");
        await store.TryIssueAsync(Phone, challenge, Policy);

        var result = await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5);

        Assert.Equal(OtpVerificationStatus.Verified, result.Status);
        Assert.Equal(challenge.ChallengeId, result.Challenge!.ChallengeId);
        Assert.Equal("Quiet Fox", result.Challenge.AnonymousName);
        Assert.Equal(OtpPurpose.Registration, result.Challenge.Purpose);
    }

    [SkippableFact]
    public async Task AVerifiedOtp_CannotBeUsedAgain()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);

        await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5);
        var again = await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5);

        Assert.Equal(OtpVerificationStatus.NotFound, again.Status);
        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
    }

    [SkippableFact]
    public async Task AWrongOtp_IsCounted_AndNeverExtendsTheExpiry()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);
        var before = (await _redis.Database.KeyTimeToLiveAsync(ChallengeKey))!.Value;
        await Task.Delay(1100);

        var result = await store.VerifyAndConsumeAsync(Phone, Hash("000000"), 5);

        Assert.Equal(OtpVerificationStatus.Invalid, result.Status);
        Assert.Equal(1, (await StoredAsync()).GetProperty("attemptCount").GetInt32());
        var after = (await _redis.Database.KeyTimeToLiveAsync(ChallengeKey))!.Value;
        Assert.True(after < before, $"TTL must keep running down, was {before} then {after}");
        // The rest of the challenge is untouched.
        Assert.Equal("Registration", (await StoredAsync()).GetProperty("purpose").GetString());
        Assert.Equal(OtpVerificationStatus.Verified, (await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5)).Status);
    }

    [SkippableFact]
    public async Task AnOtp_ExpiresWithTheRedisTtl()
    {
        var store = await StartAsync();
        var policy = new OtpPolicy { Ttl = TimeSpan.FromSeconds(1), ResendCooldown = TimeSpan.FromMilliseconds(100) };
        await store.TryIssueAsync(Phone, Challenge("482913"), policy);

        await Task.Delay(1500);
        var result = await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5);

        Assert.Equal(OtpVerificationStatus.NotFound, result.Status);
        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
    }

    [SkippableFact]
    public async Task VerifyingANumber_WithNoOtp_IsNotFound()
    {
        var store = await StartAsync();

        var result = await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5);

        Assert.Equal(OtpVerificationStatus.NotFound, result.Status);
    }

    [SkippableFact]
    public async Task BruteForce_IsStoppedAtTheAttemptLimit_EvenForTheCorrectOtp()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);

        var wrong = new List<OtpVerificationStatus>();
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            wrong.Add((await store.VerifyAndConsumeAsync(Phone, Hash($"00000{attempt}"), 5)).Status);
        }

        Assert.Equal(
            [OtpVerificationStatus.Invalid, OtpVerificationStatus.Invalid, OtpVerificationStatus.Invalid, OtpVerificationStatus.Invalid, OtpVerificationStatus.AttemptsExceeded],
            wrong);
        // Locked out: the right code no longer works either, only a new OTP does.
        Assert.Equal(OtpVerificationStatus.AttemptsExceeded, (await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5)).Status);
        Assert.Equal(5, (await StoredAsync()).GetProperty("attemptCount").GetInt32());
    }

    [SkippableFact]
    public async Task ANewOtp_ResetsTheAttemptCount_AfterALockout()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), QuickCooldown);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await store.VerifyAndConsumeAsync(Phone, Hash("000000"), 5);
        }

        await Task.Delay(400);
        await store.TryIssueAsync(Phone, Challenge("654321"), QuickCooldown);

        Assert.Equal(OtpVerificationStatus.Verified, (await store.VerifyAndConsumeAsync(Phone, Hash("654321"), 5)).Status);
    }

    [SkippableFact]
    public async Task ParallelGuesses_AllCountTowardsTheLimit()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);

        var results = await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            Task.Run(() => store.VerifyAndConsumeAsync(Phone, Hash($"9{i:D5}"), 5))));

        Assert.Equal(4, results.Count(r => r.Status == OtpVerificationStatus.Invalid));
        Assert.Equal(36, results.Count(r => r.Status == OtpVerificationStatus.AttemptsExceeded));
        Assert.Equal(5, (await StoredAsync()).GetProperty("attemptCount").GetInt32());
        Assert.Equal(OtpVerificationStatus.AttemptsExceeded, (await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5)).Status);
    }

    [SkippableFact]
    public async Task ConcurrentVerificationsOfTheCorrectOtp_SucceedExactlyOnce()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);

        var results = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ =>
            Task.Run(() => store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5))));

        Assert.Equal(1, results.Count(r => r.Status == OtpVerificationStatus.Verified));
        Assert.Equal(24, results.Count(r => r.Status == OtpVerificationStatus.NotFound));
    }

    [SkippableFact]
    public async Task GuessesMixedWithTheRightOtp_NeverLetTwoVerificationsThrough()
    {
        var store = await StartAsync();
        await store.TryIssueAsync(Phone, Challenge("482913"), Policy);

        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(i =>
            Task.Run(() => store.VerifyAndConsumeAsync(Phone, Hash(i % 3 == 0 ? "482913" : $"1{i:D5}"), 5))));

        // Zero is possible (enough wrong guesses can arrive first and lock the OTP); two never is.
        Assert.InRange(results.Count(r => r.Status == OtpVerificationStatus.Verified), 0, 1);
    }

    // ---- rollback after a failed delivery ------------------------------------------------------------------

    [SkippableFact]
    public async Task RevokingAnUndeliveredOtp_MakesItUnusable_AndFreesTheNumberToTryAgain()
    {
        var store = await StartAsync();
        var challenge = Challenge("482913");
        await store.TryIssueAsync(Phone, challenge, Policy);

        var revoked = await store.RevokeUndeliveredAsync(Phone, challenge.ChallengeId);

        Assert.True(revoked);
        Assert.Equal(OtpVerificationStatus.NotFound, (await store.VerifyAndConsumeAsync(Phone, Hash("482913"), 5)).Status);
        // No cooldown is left behind, and the failed send did not use up the hourly allowance.
        Assert.False(await _redis.Database.KeyExistsAsync(SendCountKey));
        Assert.Equal(OtpIssueStatus.Issued, (await store.TryIssueAsync(Phone, Challenge(), Policy)).Status);
    }

    [SkippableFact]
    public async Task Revoking_NeverRemovesANewerOtp()
    {
        var store = await StartAsync();
        var stale = Challenge("111111");
        await store.TryIssueAsync(Phone, stale, QuickCooldown);
        await Task.Delay(400);
        var newer = Challenge("222222");
        await store.TryIssueAsync(Phone, newer, QuickCooldown);

        var revoked = await store.RevokeUndeliveredAsync(Phone, stale.ChallengeId);

        Assert.False(revoked);
        Assert.Equal(newer.ChallengeId.ToString(), (await StoredAsync()).GetProperty("challengeId").GetString());
        Assert.Equal("2", (string?)await _redis.Database.StringGetAsync(SendCountKey)); // no refund for a challenge that was not removed
        Assert.Equal(OtpVerificationStatus.Verified, (await store.VerifyAndConsumeAsync(Phone, Hash("222222"), 5)).Status);
    }

    [SkippableFact]
    public async Task Revoking_AChallengeThatIsAlreadyGone_IsHarmless()
    {
        var store = await StartAsync();

        Assert.False(await store.RevokeUndeliveredAsync(Phone, Guid.NewGuid()));
    }

    [SkippableFact]
    public async Task ConcurrentRevokes_OfTheSameChallenge_RefundTheSendOnlyOnce()
    {
        var store = await StartAsync();
        var policy = new OtpPolicy { ResendCooldown = TimeSpan.FromMilliseconds(1) };
        var earlier = Challenge("111111");
        await store.TryIssueAsync(Phone, earlier, policy);
        await Task.Delay(30);
        var challenge = Challenge("222222");
        await store.TryIssueAsync(Phone, challenge, policy);

        var revokes = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            Task.Run(() => store.RevokeUndeliveredAsync(Phone, challenge.ChallengeId))));

        Assert.Equal(1, revokes.Count(revoked => revoked));
        Assert.Equal("1", (string?)await _redis.Database.StringGetAsync(SendCountKey));
    }

    // ---- the pre-existing operations, now script-based ------------------------------------------------------

    [SkippableFact]
    public async Task SetAndGet_RoundTrip_AndExpire()
    {
        var store = await StartAsync();
        var challenge = Challenge("111111", name: "Quiet Fox");

        await store.SetAsync(Phone, challenge, TimeSpan.FromSeconds(30));
        var read = await store.GetAsync(Phone);

        Assert.NotNull(read);
        Assert.Equal(challenge.ChallengeId, read!.ChallengeId);
        Assert.Equal("Quiet Fox", read.AnonymousName);
        var ttl = await _redis.Database.KeyTimeToLiveAsync(ChallengeKey);
        Assert.InRange(ttl!.Value.TotalSeconds, 28, 30);
        Assert.Null(await store.GetAsync("+919000000000"));
    }

    [SkippableFact]
    public async Task ReplaceKeepingTtl_KeepsTheRemainingTime_AndRefusesWhenTheOtpIsGone()
    {
        var store = await StartAsync();
        Assert.False(await store.ReplaceKeepingTtlAsync(Phone, Challenge()));
        await store.SetAsync(Phone, Challenge(), TimeSpan.FromSeconds(30));
        var replacement = Challenge("222222");

        var replaced = await store.ReplaceKeepingTtlAsync(Phone, replacement);

        Assert.True(replaced);
        Assert.Equal(replacement.ChallengeId, (await store.GetAsync(Phone))!.ChallengeId);
        Assert.InRange((await _redis.Database.KeyTimeToLiveAsync(ChallengeKey))!.Value.TotalSeconds, 27, 30);
    }

    [SkippableFact]
    public async Task DeleteIfChallengeMatches_OnlyDeletesTheNamedChallenge()
    {
        var store = await StartAsync();
        var challenge = Challenge();
        await store.SetAsync(Phone, challenge, TimeSpan.FromSeconds(30));

        Assert.False(await store.DeleteIfChallengeMatchesAsync(Phone, Guid.NewGuid()));
        Assert.NotNull(await store.GetAsync(Phone));
        Assert.True(await store.DeleteIfChallengeMatchesAsync(Phone, challenge.ChallengeId));
        Assert.Null(await store.GetAsync(Phone));
        await store.DeleteAsync(Phone); // deleting nothing is fine
    }

    // ---- Redis failure ------------------------------------------------------------------------------------

    [Fact]
    public async Task WhenRedisIsDown_EveryOperationFailsLoudly_NeverSilently()
    {
        var store = new RedisOtpChallengeStore(new UnavailableScriptClient());

        await Assert.ThrowsAsync<HttpRequestException>(() => store.TryIssueAsync(Phone, Challenge(), Policy));
        await Assert.ThrowsAsync<HttpRequestException>(() => store.VerifyAndConsumeAsync(Phone, Hash("111111"), 5));
        await Assert.ThrowsAsync<HttpRequestException>(() => store.RevokeUndeliveredAsync(Phone, Guid.NewGuid()));
    }
}
