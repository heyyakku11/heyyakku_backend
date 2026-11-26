using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Sms;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Infrastructure;

namespace Yakku.Tests;

/// <summary>
/// The review (test) account: one configured number signs in with a fixed 6-digit OTP and is never sent an SMS,
/// while every other number, and the review number when the flag is off, keep the normal flow. Runs against an
/// in-memory challenge store, so it needs no Docker.
/// </summary>
public sealed class ReviewAccountTests
{
    private const string ReviewPhone = "+919999990001";
    private const string ReviewCode = "482913";
    private const string OtherPhone = "+919876543210";

    // What the (random) generator hands out for everyone who is not the review account.
    private const string GeneratedOtp = "135790";

    private static ReviewAccountOptions Enabled(string phone = ReviewPhone, string code = ReviewCode)
        => ReviewAccountOptions.Create("true", phone, code);

    private static RequestOtpRequest Request(string phone) => new() { PhoneNumber = phone };

    private static VerifyOtpRequest Verify(string phone, string otp) => new() { PhoneNumber = phone, Otp = otp };

    // ---- the review account ---------------------------------------------------------------------------------

    [Fact]
    public async Task ReviewPhone_GetsTheFixedCode_AndNoSmsIsSent()
    {
        var h = new Harness(Enabled());

        var response = await h.Auth().RequestOtpAsync(Request(ReviewPhone));

        Assert.Equal("Registration", response.Purpose);
        Assert.Empty(h.Queue.Jobs);
        Assert.Empty(h.Sms.Sent);
        Assert.Equal(0, h.Generator.Calls);
        // With SMS on, the challenge holds the hash of the fixed code, exactly as it would for a texted OTP.
        Assert.Equal(OtpHasher.Hash(ReviewPhone, ReviewCode), h.Store.Get(ReviewPhone)!.OtpHash);
    }

    [Fact]
    public async Task ReviewPhone_CanRegister_ThenLogInAgain_WithTheFixedCode()
    {
        var h = new Harness(Enabled());

        await h.Auth().RequestOtpAsync(Request(ReviewPhone));
        var registered = await h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode));

        Assert.Equal("Registration", registered.Purpose);
        Assert.Single(h.Users.Snapshot);
        Assert.Equal(ReviewPhone, h.Users.Snapshot[0].PhoneNumber);

        var login = await h.Auth().RequestOtpAsync(Request(ReviewPhone));
        var loggedIn = await h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode));

        Assert.Equal("Login", login.Purpose);
        Assert.Equal("Login", loggedIn.Purpose);
        Assert.Single(h.Users.Snapshot);
        Assert.Empty(h.Queue.Jobs);
    }

    [Fact]
    public async Task ReviewPhone_WithSmsDisabled_StoresTheFixedCodeInPlaintext_AndVerifies()
    {
        var h = new Harness(Enabled()) { SmsEnabled = false };

        await h.Auth().RequestOtpAsync(Request(ReviewPhone));

        Assert.Equal(ReviewCode, h.Store.Get(ReviewPhone)!.OtpHash);
        var verified = await h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode));
        Assert.Equal("Registration", verified.Purpose);
    }

    [Fact]
    public async Task ReviewPhone_StillRejectsAWrongCode()
    {
        var h = new Harness(Enabled());
        await h.Auth().RequestOtpAsync(Request(ReviewPhone));

        var exception = await Assert.ThrowsAsync<AppException>(
            () => h.Auth().VerifyOtpAsync(Verify(ReviewPhone, "000000")));

        Assert.Equal(ApiErrorCodes.OtpInvalid, exception.ErrorCode);
        Assert.Empty(h.Users.Snapshot);
    }

    [Fact]
    public async Task ReviewPhone_StillNeedsAnOtpToBeRequestedFirst()
    {
        var h = new Harness(Enabled());

        var exception = await Assert.ThrowsAsync<AppException>(
            () => h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode)));

        Assert.Equal(ApiErrorCodes.OtpNotFound, exception.ErrorCode);
        Assert.Empty(h.Users.Snapshot);
    }

    [Fact]
    public async Task ReviewPhone_GoesThroughTheStoreLimits_SoTheCooldownAndSendLimitStillApply()
    {
        var h = new Harness(Enabled());
        h.Store.NextIssueStatus = OtpIssueStatus.CooldownActive;

        var cooldown = await Assert.ThrowsAsync<AppException>(() => h.Auth().RequestOtpAsync(Request(ReviewPhone)));
        Assert.Equal(ApiErrorCodes.OtpResendCooldown, cooldown.ErrorCode);

        h.Store.NextIssueStatus = OtpIssueStatus.SendLimitReached;
        var limited = await Assert.ThrowsAsync<AppException>(() => h.Auth().RequestOtpAsync(Request(ReviewPhone)));
        Assert.Equal(ApiErrorCodes.RateLimited, limited.ErrorCode);

        Assert.Equal(2, h.Store.IssueCalls);
        Assert.Null(h.Store.Get(ReviewPhone));
    }

    [Fact]
    public async Task TheFixedCodeAndTheFullPhone_AreNeverLogged()
    {
        var h = new Harness(Enabled());

        await h.Auth().RequestOtpAsync(Request(ReviewPhone));
        await h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode));

        Assert.Contains("Review account", h.Log.AllText);
        Assert.DoesNotContain(ReviewCode, h.Log.AllText);
        Assert.DoesNotContain(ReviewPhone, h.Log.AllText);
    }

    // ---- everyone else, and the flag off --------------------------------------------------------------------

    [Fact]
    public async Task AnyOtherNumber_KeepsTheNormalFlow_WithTheGeneratedOtpSentBySms()
    {
        var h = new Harness(Enabled());

        await h.Auth().RequestOtpAsync(Request(OtherPhone));

        var job = Assert.Single(h.Queue.Jobs);
        Assert.Equal(OtherPhone, job.PhoneNumber);
        Assert.Equal(GeneratedOtp, job.Otp);
        Assert.Equal(1, h.Generator.Calls);

        // The review code does not unlock another number.
        var exception = await Assert.ThrowsAsync<AppException>(
            () => h.Auth().VerifyOtpAsync(Verify(OtherPhone, ReviewCode)));
        Assert.Equal(ApiErrorCodes.OtpInvalid, exception.ErrorCode);
        Assert.Empty(h.Users.Snapshot);

        var verified = await h.Auth().VerifyOtpAsync(Verify(OtherPhone, GeneratedOtp));
        Assert.Equal("Registration", verified.Purpose);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("0")]
    public async Task WithTheFlagOff_TheReviewPhoneFollowsTheNormalFlow(string? enabled)
    {
        var h = new Harness(ReviewAccountOptions.Create(enabled, ReviewPhone, ReviewCode));

        await h.Auth().RequestOtpAsync(Request(ReviewPhone));

        var job = Assert.Single(h.Queue.Jobs);
        Assert.Equal(GeneratedOtp, job.Otp);
        Assert.Equal(1, h.Generator.Calls);
        var exception = await Assert.ThrowsAsync<AppException>(
            () => h.Auth().VerifyOtpAsync(Verify(ReviewPhone, ReviewCode)));
        Assert.Equal(ApiErrorCodes.OtpInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task WithoutAnyReviewConfiguration_TheServiceBehavesAsBefore()
    {
        var h = new Harness(reviewAccount: null);

        await h.Auth().RequestOtpAsync(Request(ReviewPhone));

        Assert.Equal(GeneratedOtp, Assert.Single(h.Queue.Jobs).Otp);
    }

    // ---- the options and startup validation -----------------------------------------------------------------

    [Fact]
    public void AGoodConfiguration_IsEnabled_AndKeepsTheNormalisedNumber()
    {
        var options = ReviewAccountOptions.Create(" TRUE ", "+91 99999-90001", ReviewCode);

        options.ThrowIfInvalid();
        Assert.True(options.Enabled);
        Assert.Equal(ReviewPhone, options.PhoneNumber);
        Assert.Equal(ReviewCode, options.Code);
        Assert.True(options.IsReviewAccount(ReviewPhone));
        Assert.False(options.IsReviewAccount(OtherPhone));
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    [InlineData("")]
    [InlineData(null)]
    public void WhenEnabled_ACodeThatIsNotSixDigits_IsRejected_WithoutEchoingIt(string? code)
    {
        var options = ReviewAccountOptions.Create("true", ReviewPhone, code);

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains(ReviewAccountOptions.CodeVariable, exception.Message);
        if (!string.IsNullOrEmpty(code))
        {
            Assert.DoesNotContain(code, exception.Message);
        }

        Assert.False(options.IsReviewAccount(ReviewPhone));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("9999990001")]
    [InlineData("+15551234567")]
    [InlineData("+915999990001")]
    [InlineData("+91999999000")]
    public void WhenEnabled_APhoneThatIsNotAnIndianMobile_IsRejected(string? phone)
    {
        var options = ReviewAccountOptions.Create("true", phone, ReviewCode);

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains(ReviewAccountOptions.PhoneNumberVariable, exception.Message);
        Assert.DoesNotContain(ReviewCode, exception.Message);
    }

    [Fact]
    public void AnUnrecognisedEnabledValue_IsRejected()
    {
        var options = ReviewAccountOptions.Create("yes", ReviewPhone, ReviewCode);

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains(ReviewAccountOptions.EnabledVariable, exception.Message);
    }

    [Fact]
    public void WhenDisabled_AHalfFilledSection_NeverStopsTheApp()
    {
        var options = ReviewAccountOptions.Create("false", "not-a-phone", "abc");

        options.ThrowIfInvalid();
        Assert.False(options.Enabled);
        Assert.False(options.IsReviewAccount("not-a-phone"));
    }

    [Fact]
    public void ToString_NeverContainsTheCodeOrTheNumber()
    {
        var text = Enabled().ToString();

        Assert.DoesNotContain(ReviewCode, text);
        Assert.DoesNotContain(ReviewPhone, text);
    }

    [Fact]
    public void Startup_RefusesBadReviewConfiguration_AndRegistersGoodConfiguration()
    {
        var bad = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Msg91:Enabled"] = "false",
                ["Otp:ReviewAccount:Enabled"] = "true",
                ["Otp:ReviewAccount:PhoneNumber"] = ReviewPhone,
                ["Otp:ReviewAccount:Code"] = "12345"
            })
            .Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddSmsDelivery(bad));

        var good = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Msg91:Enabled"] = "false",
                ["Otp:ReviewAccount:Enabled"] = "true",
                ["Otp:ReviewAccount:PhoneNumber"] = ReviewPhone,
                ["Otp:ReviewAccount:Code"] = ReviewCode
            })
            .Build();
        using var provider = new ServiceCollection().AddSmsDelivery(good).BuildServiceProvider();
        var registered = provider.GetRequiredService<ReviewAccountOptions>();
        Assert.True(registered.IsReviewAccount(ReviewPhone));

        var off = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Msg91:Enabled"] = "false" })
            .Build();
        using var offProvider = new ServiceCollection().AddSmsDelivery(off).BuildServiceProvider();
        Assert.False(offProvider.GetRequiredService<ReviewAccountOptions>().Enabled);
    }

    [Fact]
    public void EnvironmentVariableNames_MapToTheConfigurationSection()
    {
        // ASP.NET turns "__" in an environment variable name into the ":" section separator.
        Assert.Equal(
            ReviewAccountOptions.SectionName + ":Enabled",
            ReviewAccountOptions.EnabledVariable.Replace("__", ":"));
        Assert.Equal(
            ReviewAccountOptions.SectionName + ":PhoneNumber",
            ReviewAccountOptions.PhoneNumberVariable.Replace("__", ":"));
        Assert.Equal(
            ReviewAccountOptions.SectionName + ":Code",
            ReviewAccountOptions.CodeVariable.Replace("__", ":"));
    }

    // ---- test doubles ---------------------------------------------------------------------------------------

    private sealed class Harness
    {
        private readonly ReviewAccountOptions? _reviewAccount;

        public Harness(ReviewAccountOptions? reviewAccount)
        {
            _reviewAccount = reviewAccount;
        }

        public bool SmsEnabled
        {
            get => Sms.IsEnabled;
            set => Sms.IsEnabled = value;
        }

        public InMemoryStore Store { get; } = new();

        public FakeSmsService Sms { get; } = new();

        public RecordingQueue Queue { get; } = new();

        public CountingGenerator Generator { get; } = new();

        public FakeUsers Users { get; } = new();

        public ThreadSafeAuditWriter Audit { get; } = new();

        public CapturingLogger<AuthService> Log { get; } = new();

        public AuthService Auth() => new(
            Users,
            Store,
            Generator,
            new FixedNameAllocator(),
            new FakeSessions(),
            new RequestOtpValidator(),
            new ResendOtpValidator(),
            new VerifyOtpValidator(),
            Audit,
            Sms,
            Queue,
            OtpPolicy.Default,
            Log,
            _reviewAccount);
    }

    private sealed class CountingGenerator : IOtpGenerator
    {
        private int _calls;

        public int Calls => _calls;

        public string Generate()
        {
            Interlocked.Increment(ref _calls);
            return GeneratedOtp;
        }
    }

    private sealed class RecordingQueue : IOtpSmsQueue
    {
        private readonly ConcurrentQueue<SmsOtpJob> _jobs = new();

        public IReadOnlyCollection<SmsOtpJob> Jobs => _jobs.ToArray();

        public ValueTask EnqueueAsync(SmsOtpJob job, CancellationToken cancellationToken = default)
        {
            _jobs.Enqueue(job);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class InMemoryStore : IOtpChallengeStore
    {
        private readonly Dictionary<string, OtpChallenge> _challenges = [];

        public OtpIssueStatus NextIssueStatus { get; set; } = OtpIssueStatus.Issued;

        public int IssueCalls { get; private set; }

        public OtpChallenge? Get(string phoneNumber) => _challenges.GetValueOrDefault(phoneNumber);

        public Task<OtpChallenge?> GetAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(Get(phoneNumber));

        public Task SetAsync(string phoneNumber, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _challenges[phoneNumber] = challenge;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(string phoneNumber, OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            _challenges[phoneNumber] = challenge;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            _challenges.Remove(phoneNumber);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteIfChallengeMatchesAsync(string phoneNumber, Guid challengeId, CancellationToken cancellationToken = default)
        {
            if (_challenges.TryGetValue(phoneNumber, out var challenge) && challenge.ChallengeId == challengeId)
            {
                _challenges.Remove(phoneNumber);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<OtpIssueResult> TryIssueAsync(string phoneNumber, OtpChallenge challenge, OtpPolicy policy, CancellationToken cancellationToken = default)
        {
            IssueCalls++;
            if (NextIssueStatus == OtpIssueStatus.Issued)
            {
                _challenges[phoneNumber] = challenge;
            }

            return Task.FromResult(new OtpIssueResult(NextIssueStatus, TimeSpan.Zero));
        }

        public Task<OtpVerificationResult> VerifyAndConsumeAsync(string phoneNumber, string otpHash, int maxAttempts, CancellationToken cancellationToken = default)
        {
            if (!_challenges.TryGetValue(phoneNumber, out var challenge))
            {
                return Task.FromResult(new OtpVerificationResult(OtpVerificationStatus.NotFound));
            }

            if (challenge.OtpHash != otpHash)
            {
                return Task.FromResult(new OtpVerificationResult(OtpVerificationStatus.Invalid));
            }

            _challenges.Remove(phoneNumber);
            return Task.FromResult(new OtpVerificationResult(OtpVerificationStatus.Verified, challenge));
        }

        public Task<bool> RevokeUndeliveredAsync(string phoneNumber, Guid challengeId, CancellationToken cancellationToken = default)
            => DeleteIfChallengeMatchesAsync(phoneNumber, challengeId, cancellationToken);
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly List<User> _users = [];

        public IReadOnlyList<User> Snapshot => _users.ToList();

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.FirstOrDefault(user => user.Id == id));

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult(_users.FirstOrDefault(user => user.PhoneNumber == phoneNumber));

        public Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _users.Add(user);
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

    private sealed class FixedNameAllocator : IAnonymousNameAllocator
    {
        public Task<string> ReserveAsync(TimeSpan holdFor, CancellationToken cancellationToken = default)
            => Task.FromResult("Quiet Fox");

        public Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
