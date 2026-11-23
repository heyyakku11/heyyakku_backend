using System.Text.Json;
using System.Threading.Channels;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Sms;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Redis;
using Yakku.Infrastructure.Sms;

namespace Yakku.Tests;

/// <summary>
/// The signup OTP pipeline end to end: validate, rate-limit, generate, store in a real Redis, deliver through the
/// (faked) MSG91 seam, then verify. Checks ordering, rollback after a failed delivery, and the concurrency
/// guarantees. Skipped when Docker is not available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class OtpSignupFlowTests
{
    private const string Phone = "+919876543210";
    private const string ChallengeKey = "auth:otp:" + Phone;
    private const string SendCountKey = "auth:otp-sends:" + Phone;

    private readonly RedisFixture _redis;

    public OtpSignupFlowTests(RedisFixture redis)
    {
        _redis = redis;
    }

    private async Task<Harness> StartAsync(OtpPolicy? policy = null, bool userExists = false)
    {
        Skip.IfNot(_redis.IsAvailable, _redis.SkipReason);
        await _redis.ResetAsync();
        return new Harness(new RedisOtpChallengeStore(_redis.Client), policy ?? OtpPolicy.Default, userExists);
    }

    private static async Task<AppException> AssertFailsAsync(Task task, int status, string code)
    {
        var exception = await Assert.ThrowsAsync<AppException>(() => task);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(code, exception.ErrorCode);
        return exception;
    }

    private static VerifyOtpRequest Verify(string otp) => new() { PhoneNumber = Phone, Otp = otp };

    private static RequestOtpRequest Request(string phone = Phone) => new() { PhoneNumber = phone };

    // Never a valid guess for the OTP the SMS carried.
    private static string WrongOtp(string otp) => otp == "000000" ? "000001" : "000000";

    // ---- the happy path ------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task Request_StoresTheOtp_SendsItBySms_AndNeverReturnsIt()
    {
        var h = await StartAsync();

        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal("Registration", response.Purpose);
        Assert.Equal(300, response.ExpiresInSeconds);
        var sms = Assert.Single(h.Sms.Sent);
        Assert.Equal(Phone, sms.PhoneNumber);
        Assert.Matches(@"^\d{6}$", sms.Otp);

        // The response never carries the OTP, and Redis never holds it in clear.
        Assert.DoesNotContain(sms.Otp, JsonSerializer.Serialize(response));
        var stored = (string)(await _redis.Database.StringGetAsync(ChallengeKey))!;
        Assert.DoesNotContain(sms.Otp, stored);
        var ttl = (await _redis.Database.KeyTimeToLiveAsync(ChallengeKey))!.Value;
        Assert.InRange(ttl.TotalSeconds, 298, 300);

        // The SMS audit row records provider acceptance and the correlation id, never the OTP.
        var audit = Assert.Single(h.Audit.Notifications);
        Assert.Equal(NotificationChannel.Sms, audit.Channel);
        Assert.Equal("msg91", audit.Provider);
        Assert.Equal(NotificationAuditStatus.Accepted, audit.Status);
        Assert.Equal(sms.CorrelationId, audit.CorrelationId);
        Assert.DoesNotContain(sms.Otp, JsonSerializer.Serialize(audit));
    }

    [SkippableFact]
    public async Task TheOtpThatWasTexted_IsTheOneThatVerifies_AndWorksOnlyOnce()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;

        var verified = await h.NewAuth().VerifyOtpAsync(Verify(otp));

        Assert.Equal("Registration", verified.Purpose);
        Assert.Single(h.Users.Snapshot);
        Assert.Equal(Phone, h.Users.Snapshot[0].PhoneNumber);
        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(otp)), 404, ApiErrorCodes.OtpNotFound);
        Assert.Single(h.Users.Snapshot);
    }

    [SkippableFact]
    public async Task AnExistingUser_GetsALoginOtp_ThroughTheSamePipeline()
    {
        var h = await StartAsync(userExists: true);

        var response = await h.NewAuth().RequestOtpAsync(Request());
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(h.Sms.Last.Otp));

        Assert.Equal("Login", response.Purpose);
        Assert.Equal("Login", verified.Purpose);
        Assert.Single(h.Users.Snapshot);
    }

    [SkippableFact]
    public async Task EveryOtp_LivesExactlyFiveMinutes_InRedisAndInTheResponse()
    {
        var h = await StartAsync();

        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal(300, response.ExpiresInSeconds);
        var ttl = (await _redis.Database.KeyTimeToLiveAsync(ChallengeKey))!.Value;
        Assert.InRange(ttl.TotalSeconds, 298, 300);
        Assert.True(ttl <= TimeSpan.FromMinutes(5));
    }

    // ---- validation ----------------------------------------------------------------------------------------

    [SkippableTheory]
    [InlineData("+919876543210x")]
    [InlineData("9876543210")]
    [InlineData("+15551234567")]
    [InlineData("+91987654321")]
    [InlineData("+915987654321")]
    [InlineData("")]
    public async Task AnInvalidNumber_IsRejectedBeforeAnythingIsStoredOrSent(string phone)
    {
        var h = await StartAsync();

        await Assert.ThrowsAsync<ValidationException>(() => h.NewAuth().RequestOtpAsync(Request(phone)));

        Assert.Empty(h.Sms.Sent);
        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
        Assert.False(await _redis.Database.KeyExistsAsync(SendCountKey));
    }

    [SkippableTheory]
    [InlineData("+91 98765 43210")]
    [InlineData("0091-98765-43210")]
    public async Task FormattingCharacters_AreNormalisedBeforeSending(string phone)
    {
        var h = await StartAsync();

        await h.NewAuth().RequestOtpAsync(Request(phone));

        Assert.Equal(Phone, h.Sms.Last.PhoneNumber);
    }

    // ---- delivery failure after the OTP was stored (async worker; API still returns 200) --------------------

    [SkippableTheory]
    [InlineData(SmsFailureCategory.Authentication, 1)]
    [InlineData(SmsFailureCategory.Template, 1)]
    [InlineData(SmsFailureCategory.Rejected, 1)]
    [InlineData(SmsFailureCategory.InvalidResponse, 1)]
    [InlineData(SmsFailureCategory.InvalidRecipient, 1)]
    [InlineData(SmsFailureCategory.Network, 3)]
    [InlineData(SmsFailureCategory.Timeout, 3)]
    [InlineData(SmsFailureCategory.ProviderError, 3)]
    [InlineData(SmsFailureCategory.ProviderRateLimited, 3)]
    public async Task WhenTheProviderDoesNotAccept_TheApiStillSucceeds_AndTheWorkerInvalidatesTheOtp(
        SmsFailureCategory failure,
        int expectedAttempts)
    {
        var h = await StartAsync();
        h.Sms.Behavior = FakeSmsService.FailsWith(failure);

        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal("Registration", response.Purpose);
        Assert.Equal(expectedAttempts, h.Sms.Sent.Count);

        var otp = h.Sms.Last.Otp;
        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
        Assert.False(await _redis.Database.KeyExistsAsync(SendCountKey));
        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(otp)), 404, ApiErrorCodes.OtpNotFound);
        Assert.Empty(h.Users.Snapshot);

        var audits = h.Audit.Notifications.Where(entry => entry.Status == NotificationAuditStatus.Failed).ToList();
        Assert.Equal(expectedAttempts, audits.Count);
        Assert.All(audits, entry => Assert.Equal(failure.ToString(), entry.ErrorCode));
        Assert.DoesNotContain(otp, JsonSerializer.Serialize(audits));
        Assert.DoesNotContain(otp, h.Log.AllText);
        Assert.DoesNotContain(otp, h.WorkerLog.AllText);
    }

    [SkippableFact]
    public async Task AfterAFailedDelivery_TheUserCanAskAgainImmediately()
    {
        var h = await StartAsync();
        h.Sms.Behavior = FakeSmsService.FailsWith(SmsFailureCategory.Authentication);
        await h.NewAuth().RequestOtpAsync(Request());

        h.Sms.Behavior = FakeSmsService.Accepts;
        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal("Registration", response.Purpose);
        Assert.Equal(2, h.Sms.Sent.Count);
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(h.Sms.Last.Otp));
        Assert.Equal("Registration", verified.Purpose);
    }

    [SkippableFact]
    public async Task AnUnexpectedSmsException_IsRetriedThenTheOtpIsInvalidated()
    {
        var h = await StartAsync();
        h.Sms.Behavior = _ => throw new InvalidOperationException("provider client blew up");

        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal("Registration", response.Purpose);
        Assert.Equal(OtpSmsQueueOptions.MaxAttempts, h.Sms.Sent.Count);
        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
        Assert.False(await _redis.Database.KeyExistsAsync(SendCountKey));
    }

    [SkippableFact]
    public async Task WhenTheCallerCancelsMidSend_TheOtpIsInvalidated()
    {
        var h = await StartAsync();
        using var cts = new CancellationTokenSource();
        h.Sms.Behavior = _ =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.NewAuth().RequestOtpAsync(Request(), cts.Token));

        Assert.False(await _redis.Database.KeyExistsAsync(ChallengeKey));
    }

    [SkippableFact]
    public async Task ARollback_NeverDeletesANewerOtpFromAConcurrentRequest()
    {
        var h = await StartAsync();
        var newer = new OtpChallenge
        {
            ChallengeId = Guid.NewGuid(),
            OtpHash = OtpHasher.Hash(Phone, "654321"),
            Purpose = OtpPurpose.Registration,
            CreatedAt = DateTime.UtcNow
        };
        h.Sms.Behavior = async _ =>
        {
            // While this job is talking to the provider, a newer OTP replaces its challenge.
            await h.Store.SetAsync(Phone, newer, TimeSpan.FromMinutes(5));
            return SmsSendResult.Failed(SmsFailureCategory.Network);
        };

        await h.NewAuth().RequestOtpAsync(Request());

        var verified = await h.NewAuth().VerifyOtpAsync(Verify("654321"));
        Assert.Equal("Registration", verified.Purpose);
    }

    [Fact]
    public async Task WhenRedisFails_NothingIsSent_AndTheErrorIsNotHidden()
    {
        var h = new Harness(new RedisOtpChallengeStore(new UnavailableScriptClient()), OtpPolicy.Default, userExists: false);

        await Assert.ThrowsAsync<HttpRequestException>(() => h.NewAuth().RequestOtpAsync(Request()));

        Assert.Empty(h.Sms.Sent);
        Assert.Empty(h.Audit.Notifications);
    }

    [SkippableFact]
    public async Task WhenRedisFailsAtTheStoreStep_NothingIsSent()
    {
        var h = await StartAsync();
        h.Store = new IssueFailingStore(h.Store);

        await Assert.ThrowsAsync<HttpRequestException>(() => h.NewAuth().RequestOtpAsync(Request()));

        Assert.Empty(h.Sms.Sent);
    }

    [SkippableFact]
    public async Task WhenRedisFailsDuringRollback_TheWorkerLogsAndLeavesTheChallengeToExpire()
    {
        var h = await StartAsync();
        h.Store = new RevokeFailingStore(h.Store);
        h.Sms.Behavior = FakeSmsService.FailsWith(SmsFailureCategory.Authentication);

        await h.NewAuth().RequestOtpAsync(Request());

        Assert.Contains("expires on its own", h.WorkerLog.AllText);
    }

    [SkippableFact]
    public async Task WhenSmsIsDisabled_OtpIsStoredInPlaintext_AndVerifyWorksWithoutSending()
    {
        var h = await StartAsync();
        h.Sms.IsEnabled = false;

        var response = await h.NewAuth().RequestOtpAsync(Request());

        Assert.Equal("Registration", response.Purpose);
        Assert.Empty(h.Sms.Sent);
        Assert.True(await _redis.Database.KeyExistsAsync(ChallengeKey));

        var raw = await _redis.Database.StringGetAsync(ChallengeKey);
        Assert.False(raw.IsNullOrEmpty);
        // Plaintext 6-digit OTP is stored in the otpHash JSON field when SMS is disabled.
        Assert.Matches(@"""otpHash""\s*:\s*""\d{6}""", raw.ToString());

        var match = System.Text.RegularExpressions.Regex.Match(raw.ToString()!, @"""otpHash""\s*:\s*""(\d{6})""");
        Assert.True(match.Success);
        var otp = match.Groups[1].Value;

        var verified = await h.NewAuth().VerifyOtpAsync(Verify(otp));
        Assert.Equal("Registration", verified.Purpose);
        Assert.Contains("stored in plaintext", h.Log.AllText, StringComparison.OrdinalIgnoreCase);
    }

    // ---- resend cooldown, rate limiting, concurrent requests ------------------------------------------------

    [SkippableFact]
    public async Task AResendInsideTheCooldown_IsRefused_NoSecondSmsIsSent_AndTheFirstOtpStillWorks()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var first = h.Sms.Last.Otp;

        await AssertFailsAsync(h.NewAuth().RequestOtpAsync(Request()), 400, ApiErrorCodes.OtpResendCooldown);
        await AssertFailsAsync(h.NewAuth().ResendOtpAsync(new ResendOtpRequest { PhoneNumber = Phone }), 400, ApiErrorCodes.OtpResendCooldown);

        Assert.Single(h.Sms.Sent);
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(first));
        Assert.Equal("Registration", verified.Purpose);
    }

    [SkippableFact]
    public async Task AfterTheCooldown_ANewOtp_ReplacesTheOldOne()
    {
        var h = await StartAsync(new OtpPolicy { ResendCooldown = TimeSpan.FromMilliseconds(150) });
        await h.NewAuth().RequestOtpAsync(Request());
        var first = h.Sms.Last.Otp;
        await Task.Delay(400);

        await h.NewAuth().ResendOtpAsync(new ResendOtpRequest { PhoneNumber = Phone });
        var second = h.Sms.Last.Otp;

        Assert.Equal(2, h.Sms.Sent.Count);
        if (first != second)
        {
            await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(first)), 400, ApiErrorCodes.OtpInvalid);
        }

        var verified = await h.NewAuth().VerifyOtpAsync(Verify(second));
        Assert.Equal("Registration", verified.Purpose);
    }

    [SkippableFact]
    public async Task ResendWithoutAnOtp_IsNotFound()
    {
        var h = await StartAsync();

        await AssertFailsAsync(h.NewAuth().ResendOtpAsync(new ResendOtpRequest { PhoneNumber = Phone }), 404, ApiErrorCodes.OtpNotFound);

        Assert.Empty(h.Sms.Sent);
    }

    [SkippableFact]
    public async Task ConcurrentRequests_ForOneNumber_SendExactlyOneSms()
    {
        var h = await StartAsync();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 25).Select(_ => Task.Run(async () =>
        {
            try
            {
                await h.NewAuth().RequestOtpAsync(Request());
                return (string?)null;
            }
            catch (AppException exception)
            {
                return exception.ErrorCode;
            }
        })));

        Assert.Equal(1, outcomes.Count(code => code is null));
        Assert.All(outcomes.Where(code => code is not null), code => Assert.Equal(ApiErrorCodes.OtpResendCooldown, code));
        var sms = Assert.Single(h.Sms.Sent);
        Assert.Equal("1", (string?)await _redis.Database.StringGetAsync(SendCountKey));
        // The one SMS carries the one OTP that is stored.
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(sms.Otp));
        Assert.Equal("Registration", verified.Purpose);
    }

    [SkippableFact]
    public async Task TheHourlyLimit_StopsAFlood_WithTooManyRequests()
    {
        var h = await StartAsync(new OtpPolicy { ResendCooldown = TimeSpan.FromMilliseconds(1), MaxSendsPerWindow = 2 });
        await h.NewAuth().RequestOtpAsync(Request());
        await Task.Delay(30);
        await h.NewAuth().RequestOtpAsync(Request());
        await Task.Delay(30);

        await AssertFailsAsync(h.NewAuth().RequestOtpAsync(Request()), 429, ApiErrorCodes.RateLimited);

        Assert.Equal(2, h.Sms.Sent.Count);
    }

    // ---- verification: failures, expiry, brute force, concurrency -------------------------------------------

    [SkippableFact]
    public async Task AWrongOtp_IsRejected_AndCounted_AndTheRightOneStillWorks()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;

        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(WrongOtp(otp))), 400, ApiErrorCodes.OtpInvalid);

        var stored = JsonDocument.Parse((string)(await _redis.Database.StringGetAsync(ChallengeKey))!).RootElement;
        Assert.Equal(1, stored.GetProperty("attemptCount").GetInt32());
        Assert.Empty(h.Users.Snapshot);
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(otp));
        Assert.Equal("Registration", verified.Purpose);
    }

    [SkippableFact]
    public async Task AnOtp_ThatWasNeverRequested_IsNotFound()
    {
        var h = await StartAsync();

        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify("123456")), 404, ApiErrorCodes.OtpNotFound);
    }

    [SkippableFact]
    public async Task AnExpiredOtp_IsNotFound_EvenWithTheRightCode()
    {
        var h = await StartAsync(new OtpPolicy { Ttl = TimeSpan.FromSeconds(1), ResendCooldown = TimeSpan.FromMilliseconds(100) });
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;

        await Task.Delay(1500);

        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(otp)), 404, ApiErrorCodes.OtpNotFound);
        Assert.Empty(h.Users.Snapshot);
    }

    [SkippableFact]
    public async Task BruteForce_LocksTheOtp_AfterFiveWrongGuesses_AndIsAuditedAsSuspicious()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;
        var wrong = WrongOtp(otp);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(wrong)), 400, ApiErrorCodes.OtpInvalid);
        }

        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(wrong)), 400, ApiErrorCodes.OtpAttemptsExceeded);
        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(otp)), 400, ApiErrorCodes.OtpAttemptsExceeded);
        Assert.Empty(h.Users.Snapshot);
        Assert.Contains(h.Audit.Authentication, entry => entry.Outcome == AuditOutcome.Suspicious);
    }

    [SkippableFact]
    public async Task ParallelGuesses_CannotExceedTheAttemptLimit()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;

        var codes = await Task.WhenAll(Enumerable.Range(0, 30).Select(i => Task.Run(async () =>
        {
            var guess = i.ToString("D6");
            if (guess == otp)
            {
                guess = "999999" == otp ? "999998" : "999999";
            }

            try
            {
                await h.NewAuth().VerifyOtpAsync(Verify(guess));
                return "OK";
            }
            catch (AppException exception)
            {
                return exception.ErrorCode;
            }
        })));

        Assert.Equal(4, codes.Count(code => code == ApiErrorCodes.OtpInvalid));
        Assert.Equal(26, codes.Count(code => code == ApiErrorCodes.OtpAttemptsExceeded));
        await AssertFailsAsync(h.NewAuth().VerifyOtpAsync(Verify(otp)), 400, ApiErrorCodes.OtpAttemptsExceeded);
    }

    [SkippableFact]
    public async Task ConcurrentVerifications_OfTheRightOtp_RegisterTheUserExactlyOnce()
    {
        var h = await StartAsync();
        await h.NewAuth().RequestOtpAsync(Request());
        var otp = h.Sms.Last.Otp;

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            try
            {
                await h.NewAuth().VerifyOtpAsync(Verify(otp));
                return "OK";
            }
            catch (AppException exception)
            {
                return exception.ErrorCode;
            }
        })));

        Assert.Equal(1, outcomes.Count(outcome => outcome == "OK"));
        Assert.Equal(19, outcomes.Count(outcome => outcome == ApiErrorCodes.OtpNotFound));
        Assert.Single(h.Users.Snapshot);
    }

    [SkippableFact]
    public async Task AVerifiedOtp_DoesNotRemoveANewerOtpRequestedAfterwards()
    {
        var h = await StartAsync(new OtpPolicy { ResendCooldown = TimeSpan.FromMilliseconds(100) });
        await h.NewAuth().RequestOtpAsync(Request());
        await h.NewAuth().VerifyOtpAsync(Verify(h.Sms.Last.Otp));
        await Task.Delay(150);

        // A user who signed up asks for a login OTP straight away; finishing the earlier sign-in must not delete it.
        await h.NewAuth().RequestOtpAsync(Request());

        Assert.True(await _redis.Database.KeyExistsAsync(ChallengeKey));
        var verified = await h.NewAuth().VerifyOtpAsync(Verify(h.Sms.Last.Otp));
        Assert.Equal("Login", verified.Purpose);
    }

    // ---- test doubles ---------------------------------------------------------------------------------------

    private sealed class Harness
    {
        private readonly Channel<SmsOtpJob> _smsChannel =
            Channel.CreateBounded<SmsOtpJob>(OtpSmsQueueOptions.Capacity);

        public Harness(IOtpChallengeStore store, OtpPolicy policy, bool userExists)
        {
            Store = store;
            Policy = policy;
            Users = new FakeUsers(userExists ? [new User(Phone, "Quiet Fox")] : []);
            WorkerLog = new CapturingLogger<OtpSmsWorker>();
            var scopeFactory = new StoreScopeFactory(() => Store);
            Worker = new OtpSmsWorker(_smsChannel, scopeFactory, Sms, WorkerLog, Audit);
            SmsQueue = new InlineOtpSmsQueue((job, token) => Worker.ProcessJobAsync(job, token));
        }

        public IOtpChallengeStore Store { get; set; }

        public OtpPolicy Policy { get; }

        public FakeUsers Users { get; }

        public FakeSmsService Sms { get; } = new();

        public IOtpSmsQueue SmsQueue { get; }

        public OtpSmsWorker Worker { get; }

        public ThreadSafeAuditWriter Audit { get; } = new();

        public CapturingLogger<AuthService> Log { get; } = new();

        public CapturingLogger<OtpSmsWorker> WorkerLog { get; }

        /// <summary>A fresh service per call, as the scoped registration gives one per request.</summary>
        public AuthService NewAuth() => new(
            Users,
            Store,
            new OtpGenerator(),
            new FixedNameAllocator(),
            new FakeSessions(),
            new RequestOtpValidator(),
            new ResendOtpValidator(),
            new VerifyOtpValidator(),
            Audit,
            Sms,
            SmsQueue,
            Policy,
            Log);
    }

    private sealed class StoreScopeFactory : IServiceScopeFactory
    {
        private readonly Func<IOtpChallengeStore> _store;

        public StoreScopeFactory(Func<IOtpChallengeStore> store) => _store = store;

        public IServiceScope CreateScope() => new StoreScope(_store());

        private sealed class StoreScope : IServiceScope, IServiceProvider
        {
            private readonly IOtpChallengeStore _store;

            public StoreScope(IOtpChallengeStore store) => _store = store;

            public IServiceProvider ServiceProvider => this;

            public object? GetService(Type serviceType)
                => serviceType == typeof(IOtpChallengeStore) ? _store : null;

            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly object _gate = new();
        private readonly List<User> _users;

        public FakeUsers(IEnumerable<User> users) => _users = [.. users];

        public IReadOnlyList<User> Snapshot
        {
            get
            {
                lock (_gate)
                {
                    return _users.ToList();
                }
            }
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Snapshot.FirstOrDefault(user => user.PhoneNumber == phoneNumber));

        public Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                _users.Add(user);
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void ClearTracking()
        {
        }
    }

    private sealed class FakeSessions : ISessionService
    {
        public Task<TokenResponse> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenResponse { AccessToken = "access", RefreshToken = "refresh" });

        public Task<TokenResponse> CreateAsync(Guid userId, Yakku.Application.Devices.DTOs.RegisterDeviceRequest? installation, CancellationToken cancellationToken = default)
            => CreateAsync(userId, cancellationToken);

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedNameAllocator : Yakku.Application.Users.Interfaces.IAnonymousNameAllocator
    {
        public Task<string> ReserveAsync(TimeSpan holdFor, CancellationToken cancellationToken = default)
            => Task.FromResult("Quiet Fox");

        public Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    /// <summary>Delegates to a real store but fails the store step, as a Redis outage in the middle of a request would.</summary>
    private class DelegatingStore : IOtpChallengeStore
    {
        private readonly IOtpChallengeStore _inner;

        protected DelegatingStore(IOtpChallengeStore inner) => _inner = inner;

        public Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
            => _inner.GetAsync(email, cancellationToken);

        public Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
            => _inner.SetAsync(email, challenge, ttl, cancellationToken);

        public Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default)
            => _inner.ReplaceKeepingTtlAsync(email, challenge, cancellationToken);

        public Task DeleteAsync(string email, CancellationToken cancellationToken = default)
            => _inner.DeleteAsync(email, cancellationToken);

        public Task<bool> DeleteIfChallengeMatchesAsync(string email, Guid challengeId, CancellationToken cancellationToken = default)
            => _inner.DeleteIfChallengeMatchesAsync(email, challengeId, cancellationToken);

        public virtual Task<OtpIssueResult> TryIssueAsync(string phoneNumber, OtpChallenge challenge, OtpPolicy policy, CancellationToken cancellationToken = default)
            => _inner.TryIssueAsync(phoneNumber, challenge, policy, cancellationToken);

        public Task<OtpVerificationResult> VerifyAndConsumeAsync(string phoneNumber, string otpHash, int maxAttempts, CancellationToken cancellationToken = default)
            => _inner.VerifyAndConsumeAsync(phoneNumber, otpHash, maxAttempts, cancellationToken);

        public virtual Task<bool> RevokeUndeliveredAsync(string phoneNumber, Guid challengeId, CancellationToken cancellationToken = default)
            => _inner.RevokeUndeliveredAsync(phoneNumber, challengeId, cancellationToken);
    }

    private sealed class IssueFailingStore : DelegatingStore
    {
        public IssueFailingStore(IOtpChallengeStore inner) : base(inner)
        {
        }

        public override Task<OtpIssueResult> TryIssueAsync(string phoneNumber, OtpChallenge challenge, OtpPolicy policy, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Redis is unavailable.");
    }

    private sealed class RevokeFailingStore : DelegatingStore
    {
        public RevokeFailingStore(IOtpChallengeStore inner) : base(inner)
        {
        }

        public override Task<bool> RevokeUndeliveredAsync(string phoneNumber, Guid challengeId, CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Redis is unavailable.");
    }
}
