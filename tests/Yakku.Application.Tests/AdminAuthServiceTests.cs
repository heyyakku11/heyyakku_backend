using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using FluentValidation;
using Yakku.Application.AdminAuth;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.AdminAuth.Validators;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.AdminAuth;

public class AdminAuthServiceTests
{
    private const string JwtSecret = "test-jwt-secret-key-32-characters!";

    [Fact]
    public async Task Register_NonHeyyakkuEmail_IsRejected()
    {
        var fixture = AdminAuthFixture.Create();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.AuthService.RegisterAsync(new AdminRegisterRequest
            {
                Email = "admin@gmail.com",
                Password = "password1"
            }));

        Assert.Contains(exception.Errors, error => error.PropertyName == "Email");
        Assert.Empty(fixture.Admins.Admins);
        Assert.Null(fixture.EmailQueue.LastJob);
    }

    [Fact]
    public async Task Register_HeyyakkuEmail_StoresChallengeAndSendsOtp()
    {
        var fixture = AdminAuthFixture.Create();

        var result = await fixture.AuthService.RegisterAsync(new AdminRegisterRequest
        {
            Email = "Admin@HeyYakku.com",
            Password = "password1"
        });

        Assert.Equal("Registration", result.Purpose);
        Assert.Equal(300, result.ExpiresInSeconds);
        Assert.Empty(fixture.Admins.Admins);
        Assert.NotNull(fixture.OtpStore.Challenge);
        Assert.Equal(OtpPurpose.Registration, fixture.OtpStore.Challenge!.Purpose);
        Assert.True(AdminPasswordHasher.Verify("password1", fixture.OtpStore.Challenge.PasswordHash!));
        Assert.Equal("123456", fixture.EmailQueue.LastJob!.Otp);
        Assert.Equal("admin@heyyakku.com", fixture.EmailQueue.LastJob.RecipientEmail);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.OtpRequested);
        Assert.DoesNotContain(
            "123456",
            JsonSerializer.Serialize(fixture.Logs.Entries.Select(entry => entry.Details)));
        Assert.DoesNotContain(
            "password1",
            JsonSerializer.Serialize(fixture.Logs.Entries.Select(entry => entry.Details)));
    }

    [Fact]
    public async Task Register_VerifiedAdmin_ReturnsConflict()
    {
        var fixture = AdminAuthFixture.Create();
        var admin = new Admin("admin@heyyakku.com", AdminPasswordHasher.Hash("password1"), AdminRole.SuperAdmin);
        admin.MarkVerified();
        fixture.Admins.Admins.Add(admin);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.AuthService.RegisterAsync(new AdminRegisterRequest
            {
                Email = "admin@heyyakku.com",
                Password = "password2"
            }));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.Conflict, exception.ErrorCode);
        Assert.Equal("email", exception.Field);
        Assert.True(AdminPasswordHasher.Verify("password1", admin.PasswordHash));
        Assert.Null(fixture.EmailQueue.LastJob);
    }

    [Fact]
    public async Task Register_UnverifiedAdmin_KeepsDatabaseRowAndStoresNewHashInRedis()
    {
        var fixture = AdminAuthFixture.Create();
        var admin = new Admin("admin@heyyakku.com", AdminPasswordHasher.Hash("password1"), AdminRole.Admin);
        fixture.Admins.Admins.Add(admin);

        await fixture.AuthService.RegisterAsync(new AdminRegisterRequest
        {
            Email = "admin@heyyakku.com",
            Password = "password2"
        });

        Assert.Single(fixture.Admins.Admins);
        Assert.False(admin.IsVerified);
        Assert.Equal(AdminStatus.Pending, admin.Status);
        Assert.Equal(AdminRole.Admin, admin.Role);
        Assert.True(AdminPasswordHasher.Verify("password1", admin.PasswordHash));
        Assert.True(AdminPasswordHasher.Verify("password2", fixture.OtpStore.Challenge!.PasswordHash!));
        Assert.Equal("123456", fixture.EmailQueue.LastJob!.Otp);
    }

    [Fact]
    public async Task VerifyOtp_ExistingUnverifiedAdmin_UpdatesPasswordAndMarksVerified()
    {
        var fixture = AdminAuthFixture.Create();
        var admin = new Admin("admin@heyyakku.com", AdminPasswordHasher.Hash("password1"), AdminRole.Admin);
        fixture.Admins.Admins.Add(admin);
        await fixture.AuthService.RegisterAsync(new AdminRegisterRequest
        {
            Email = "admin@heyyakku.com",
            Password = "password2"
        });

        await fixture.AuthService.VerifyOtpAsync(new AdminVerifyOtpRequest
        {
            Email = "admin@heyyakku.com",
            Otp = "123456"
        });

        Assert.Single(fixture.Admins.Admins);
        Assert.Equal(AdminRole.Admin, admin.Role);
        Assert.True(admin.IsVerified);
        Assert.Equal(AdminStatus.Active, admin.Status);
        Assert.True(AdminPasswordHasher.Verify("password2", admin.PasswordHash));
        Assert.Null(fixture.OtpStore.Challenge);
    }

    [Fact]
    public async Task VerifyOtp_MarksAdminVerifiedAndReturnsTokens()
    {
        var fixture = AdminAuthFixture.Create();
        await fixture.AuthService.RegisterAsync(new AdminRegisterRequest
        {
            Email = "admin@heyyakku.com",
            Password = "password1"
        });

        Assert.Empty(fixture.Admins.Admins);

        var result = await fixture.AuthService.VerifyOtpAsync(new AdminVerifyOtpRequest
        {
            Email = "admin@heyyakku.com",
            Otp = "123456"
        });

        var admin = Assert.Single(fixture.Admins.Admins);
        Assert.Equal("admin@heyyakku.com", admin.Email);
        Assert.Equal(AdminRole.SuperAdmin, admin.Role);
        Assert.True(AdminPasswordHasher.Verify("password1", admin.PasswordHash));
        Assert.True(admin.IsVerified);
        Assert.Equal(AdminStatus.Active, admin.Status);
        Assert.NotNull(admin.VerifiedAt);
        Assert.NotNull(admin.LastLoginAt);
        Assert.Null(fixture.OtpStore.Challenge);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(900, result.AccessTokenExpiresInSeconds);
        Assert.Equal(604800, result.RefreshTokenExpiresInSeconds);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.AdminRegistered);

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken).Claims;
        Assert.Contains(claims, claim => claim.Type == AuthClaimTypes.Actor && claim.Value == AuthClaimTypes.AdminActor);
        Assert.Contains(claims, claim => claim.Type == JwtRegisteredClaimNames.Email && claim.Value == admin.Email);
        Assert.Contains(claims, claim => claim.Type == JwtRegisteredClaimNames.Sub && claim.Value == admin.Id.ToString());
    }

    [Fact]
    public async Task Refresh_RotatesSecretAndRejectsOldToken()
    {
        var fixture = AdminAuthFixture.Create();
        await fixture.AuthService.RegisterAsync(new AdminRegisterRequest
        {
            Email = "admin@heyyakku.com",
            Password = "password1"
        });
        var created = await fixture.AuthService.VerifyOtpAsync(new AdminVerifyOtpRequest
        {
            Email = "admin@heyyakku.com",
            Otp = "123456"
        });

        var refreshed = await fixture.SessionService.RefreshAsync(created.RefreshToken);

        Assert.NotEqual(created.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.AdminSessionRefreshed);
        Assert.True(RefreshTokenHasher.TryParse(refreshed.RefreshToken, out var sessionId, out var secret));
        Assert.True(RefreshTokenHasher.Verify(
            string.Empty,
            secret,
            fixture.Sessions.Items.Single(session => session.Id == sessionId).RefreshTokenHash));

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.SessionService.RefreshAsync(created.RefreshToken));

        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Equal(401, exception.StatusCode);
    }

    private sealed class AdminAuthFixture
    {
        public FakeAdminRepository Admins { get; }
        public FakeAdminSessionRepository Sessions { get; }
        public InMemoryAdminOtpStore OtpStore { get; }
        public CapturingEmailQueue EmailQueue { get; }
        public FakeSystemLogWriter Logs { get; }
        public AdminAuthService AuthService { get; }
        public AdminSessionService SessionService { get; }

        private AdminAuthFixture(
            FakeAdminRepository admins,
            FakeAdminSessionRepository sessions,
            InMemoryAdminOtpStore otpStore,
            CapturingEmailQueue emailQueue,
            FakeSystemLogWriter logs,
            AdminAuthService authService,
            AdminSessionService sessionService)
        {
            Admins = admins;
            Sessions = sessions;
            OtpStore = otpStore;
            EmailQueue = emailQueue;
            Logs = logs;
            AuthService = authService;
            SessionService = sessionService;
        }

        public static AdminAuthFixture Create()
        {
            var admins = new FakeAdminRepository();
            var sessions = new FakeAdminSessionRepository();
            var otpStore = new InMemoryAdminOtpStore();
            var emailQueue = new CapturingEmailQueue();
            var emailLogs = new FakeEmailLogRepository();
            var logs = new FakeSystemLogWriter();
            var tokenService = new TokenService(JwtSecret);
            var sessionService = new AdminSessionService(
                sessions,
                admins,
                tokenService,
                logs,
                new RefreshTokenValidator());
            var authService = new AdminAuthService(
                admins,
                otpStore,
                new FakeOtpGenerator(),
                emailLogs,
                emailQueue,
                sessionService,
                logs,
                new AdminRegisterValidator(),
                new AdminVerifyOtpValidator(),
                new AdminLoginValidator());

            return new AdminAuthFixture(
                admins,
                sessions,
                otpStore,
                emailQueue,
                logs,
                authService,
                sessionService);
        }
    }

    private sealed class FakeAdminRepository : IAdminRepository
    {
        private Admin? _pending;

        public List<Admin> Admins { get; } = [];

        public Task<Admin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Admins.FirstOrDefault(admin => admin.Id == id));
        }

        public Task<Admin?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Admins.FirstOrDefault(admin => admin.Email == email));
        }

        public Task AddAsync(Admin admin, CancellationToken cancellationToken = default)
        {
            _pending = admin;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_pending is not null && Admins.All(admin => admin.Id != _pending.Id))
            {
                Admins.Add(_pending);
            }

            _pending = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdminSessionRepository : IAdminSessionRepository
    {
        private AdminSession? _pending;

        public List<AdminSession> Items { get; } = [];

        public Task AddAsync(AdminSession session, CancellationToken cancellationToken = default)
        {
            _pending = session;
            return Task.CompletedTask;
        }

        public Task<AdminSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(session => session.Id == id));
        }

        public Task DeleteAsync(AdminSession session, CancellationToken cancellationToken = default)
        {
            Items.Remove(session);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_pending is not null && Items.All(session => session.Id != _pending.Id))
            {
                Items.Add(_pending);
            }

            _pending = null;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAdminOtpStore : IAdminOtpChallengeStore
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

        public Task SetAsync(
            string email,
            OtpChallenge challenge,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
        {
            Challenge = challenge;
            ExpiresAt = DateTime.UtcNow.Add(ttl);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(
            string email,
            OtpChallenge challenge,
            CancellationToken cancellationToken = default)
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

    private sealed class FakeOtpGenerator : IOtpGenerator
    {
        public string Generate() => "123456";
    }

    private sealed class CapturingEmailQueue : IOtpEmailQueue
    {
        public EmailJob? LastJob { get; private set; }

        public ValueTask EnqueueAsync(EmailJob job, CancellationToken cancellationToken = default)
        {
            LastJob = job;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeEmailLogRepository : IEmailLogRepository
    {
        public List<EmailLog> Logs { get; } = [];

        public Task AddAsync(EmailLog emailLog, CancellationToken cancellationToken = default)
        {
            Logs.Add(emailLog);
            return Task.CompletedTask;
        }

        public Task<EmailLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Logs.FirstOrDefault(log => log.Id == id));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
