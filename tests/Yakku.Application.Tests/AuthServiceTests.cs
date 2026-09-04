using System.Text.Json;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.Auth;

public class AuthServiceTests
{
    [Fact]
    public async Task RequestOtp_ForNewEmail_StoresRegistrationChallenge()
    {
        var fixture = AuthFixture.Create();

        var result = await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });

        Assert.Equal("Registration", result.Purpose);
        Assert.Equal(300, result.ExpiresInSeconds);
        Assert.NotNull(fixture.OtpStore.Challenge);
        Assert.Equal(OtpPurpose.Registration, fixture.OtpStore.Challenge!.Purpose);
        Assert.Equal("yakku@1233", fixture.OtpStore.Challenge.DisplayName);
        Assert.Equal("new@example.com", fixture.EmailSender.LastEmail);
        Assert.Equal("123456", fixture.EmailSender.LastOtp);
        Assert.Empty(fixture.Users.Users);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.OtpRequested);
        Assert.DoesNotContain(
            "123456",
            JsonSerializer.Serialize(fixture.Logs.Entries.Select(entry => entry.Details)));
    }

    [Fact]
    public async Task RequestOtp_ForExistingEmail_StoresLoginChallenge()
    {
        var fixture = AuthFixture.Create();
        fixture.Users.Users.Add(new User("existing@example.com", "yakku@9999"));

        var result = await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "existing@example.com" });

        Assert.Equal("Login", result.Purpose);
        Assert.NotNull(fixture.OtpStore.Challenge);
        Assert.Equal(OtpPurpose.Login, fixture.OtpStore.Challenge!.Purpose);
        Assert.Null(fixture.OtpStore.Challenge.DisplayName);
    }

    [Fact]
    public async Task VerifyOtp_Registration_CreatesUserAndProfile()
    {
        var fixture = AuthFixture.Create();
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });

        var result = await fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
        {
            Email = "new@example.com",
            Otp = "123456"
        });

        Assert.Equal("Registration", result.Purpose);
        Assert.Equal("new@example.com", result.Email);
        Assert.Equal("yakku@1233", result.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(900, result.AccessTokenExpiresInSeconds);
        Assert.Equal(604800, result.RefreshTokenExpiresInSeconds);
        Assert.Single(fixture.Users.Users);
        Assert.Equal("yakku@1233", fixture.Users.Users[0].Profile.DisplayName);
        Assert.Null(fixture.OtpStore.Challenge);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.UserRegistered);
    }

    [Fact]
    public async Task VerifyOtp_Login_FindsExistingUserWithoutCreatingDuplicate()
    {
        var fixture = AuthFixture.Create();
        var existing = new User("existing@example.com", "yakku@9999");
        fixture.Users.Users.Add(existing);
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "existing@example.com" });

        var result = await fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
        {
            Email = "existing@example.com",
            Otp = "123456"
        });

        Assert.Equal("Login", result.Purpose);
        Assert.Equal(existing.Id, result.Id);
        Assert.Equal("yakku@9999", result.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Single(fixture.Users.Users);
        Assert.NotNull(existing.LastLoginAt);
        Assert.Null(fixture.OtpStore.Challenge);
    }

    [Fact]
    public async Task VerifyOtp_InvalidCode_DoesNotCreateUser()
    {
        var fixture = AuthFixture.Create();
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = "new@example.com",
                Otp = "000000"
            }));

        Assert.Equal(ApiErrorCodes.OtpInvalid, exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(fixture.Users.Users);
        Assert.Equal(1, fixture.OtpStore.Challenge!.AttemptCount);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.OtpInvalid);
        Assert.DoesNotContain(
            "000000",
            JsonSerializer.Serialize(fixture.Logs.Entries.Select(entry => entry.Details)));
        Assert.DoesNotContain(
            "123456",
            JsonSerializer.Serialize(fixture.Logs.Entries.Select(entry => entry.Details)));
    }

    [Fact]
    public async Task VerifyOtp_ExpiredChallenge_ReturnsFailure()
    {
        var fixture = AuthFixture.Create();
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });
        fixture.OtpStore.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = "new@example.com",
                Otp = "123456"
            }));

        Assert.Equal(ApiErrorCodes.OtpNotFound, exception.ErrorCode);
        Assert.Equal(404, exception.StatusCode);
        Assert.Empty(fixture.Users.Users);
    }

    [Fact]
    public async Task VerifyOtp_DuplicateDisplayName_ReturnsConflict()
    {
        var fixture = AuthFixture.Create();
        fixture.Users.ThrowDisplayNameConflict = true;
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = "new@example.com",
                Otp = "123456"
            }));

        Assert.Equal(ApiErrorCodes.Conflict, exception.ErrorCode);
        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("displayName", exception.Field);
        Assert.Empty(fixture.Users.Users);
    }

    [Fact]
    public async Task RequestOtp_DuringCooldown_ReturnsFailure()
    {
        var fixture = AuthFixture.Create();
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" }));

        Assert.Equal(ApiErrorCodes.OtpResendCooldown, exception.ErrorCode);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_AttemptsExceeded_ReturnsFailure()
    {
        var fixture = AuthFixture.Create();
        await fixture.Service.RequestOtpAsync(new RequestOtpRequest { Email = "new@example.com" });
        fixture.OtpStore.Challenge!.AttemptCount = OtpOptions.MaxVerificationAttempts;

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.VerifyOtpAsync(new VerifyOtpRequest
            {
                Email = "new@example.com",
                Otp = "123456"
            }));

        Assert.Equal(ApiErrorCodes.OtpAttemptsExceeded, exception.ErrorCode);
        Assert.Empty(fixture.Users.Users);
    }

    private sealed class AuthFixture
    {
        public FakeUserRepository Users { get; }
        public InMemoryOtpStore OtpStore { get; }
        public FakeOtpGenerator OtpGenerator { get; }
        public FakeDisplayNameGenerator DisplayNameGenerator { get; }
        public CapturingEmailSender EmailSender { get; }
        public FakeSystemLogWriter Logs { get; }
        public AuthService Service { get; }

        private AuthFixture(
            FakeUserRepository users,
            InMemoryOtpStore otpStore,
            FakeOtpGenerator otpGenerator,
            FakeDisplayNameGenerator displayNameGenerator,
            CapturingEmailSender emailSender,
            FakeSystemLogWriter logs,
            AuthService service)
        {
            Users = users;
            OtpStore = otpStore;
            OtpGenerator = otpGenerator;
            DisplayNameGenerator = displayNameGenerator;
            EmailSender = emailSender;
            Logs = logs;
            Service = service;
        }

        public static AuthFixture Create()
        {
            var users = new FakeUserRepository();
            var otpStore = new InMemoryOtpStore();
            var otpGenerator = new FakeOtpGenerator();
            var displayNameGenerator = new FakeDisplayNameGenerator();
            var emailSender = new CapturingEmailSender();
            var sessionService = new FakeSessionService();
            var logs = new FakeSystemLogWriter();
            var service = new AuthService(
                users,
                otpStore,
                otpGenerator,
                displayNameGenerator,
                emailSender,
                sessionService,
                logs,
                new RequestOtpValidator(),
                new VerifyOtpValidator());

            return new AuthFixture(users, otpStore, otpGenerator, displayNameGenerator, emailSender, logs, service);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private User? _pending;

        public List<User> Users { get; } = [];
        public bool ThrowDisplayNameConflict { get; set; }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Email == email));
        }

        public Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.Any(user => user.Profile.DisplayName == displayName));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            _pending = user;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowDisplayNameConflict)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Display name already exists.",
                    "displayName");
            }

            if (_pending is not null)
            {
                Users.Add(_pending);
                _pending = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryOtpStore : IOtpChallengeStore
    {
        public OtpChallenge? Challenge { get; set; }
        public DateTime? ExpiresAt { get; set; }

        public Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
        {
            if (ExpiresAt is not null && DateTime.UtcNow >= ExpiresAt)
            {
                Challenge = null;
            }

            return Task.FromResult(Challenge);
        }

        public Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            Challenge = challenge;
            ExpiresAt = DateTime.UtcNow.Add(ttl);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            if (Challenge is null || (ExpiresAt is not null && DateTime.UtcNow >= ExpiresAt))
            {
                Challenge = null;
                return Task.FromResult(false);
            }

            Challenge = challenge;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string email, CancellationToken cancellationToken = default)
        {
            Challenge = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessionService : ISessionService
    {
        public Task<TokenResponse> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TokenResponse
            {
                AccessToken = $"access-{userId:N}",
                RefreshToken = $"refresh-{userId:N}",
                AccessTokenExpiresInSeconds = JwtOptions.AccessTokenExpiresInSeconds,
                RefreshTokenExpiresInSeconds = JwtOptions.RefreshTokenExpiresInSeconds
            });
        }

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private sealed class FakeOtpGenerator : IOtpGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class FakeDisplayNameGenerator : IDisplayNameGenerator
    {
        public string Generate() => "yakku@1233";
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastEmail { get; private set; }
        public string? LastOtp { get; private set; }

        public Task SendOtpAsync(string email, string otp, CancellationToken cancellationToken = default)
        {
            LastEmail = email;
            LastOtp = otp;
            return Task.CompletedTask;
        }
    }
}
