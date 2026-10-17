using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.AdminAuth.Validators;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class AdminAuthFlowTests
{
    private const string Secret = "correct-horse-battery";
    private const string Password = "S3cure-Passw0rd";

    [Fact]
    public async Task Register_WithTheRightSecret_AcceptsAnyEmailDomain_KeepsATemporaryRecord_AndSendsOneEmail()
    {
        var h = new Harness();

        var response = await h.Service.RegisterAsync(Register("Someone@Gmail.com", Secret));

        Assert.Equal(10 * 60, response.ExpiresInSeconds);
        var pending = await h.Store.GetPendingAsync("someone@gmail.com");
        Assert.NotNull(pending);
        Assert.NotEqual(Password, pending!.PasswordHash);
        Assert.Empty(h.Admins.All);

        var job = Assert.Single(h.Emails.Jobs);
        Assert.Equal(AdminEmailTemplate.Registration, job.Template);
        Assert.Equal("someone@gmail.com", job.RecipientEmail);
        Assert.Equal("someone@gmail.com", job.Variables[AdminEmailVariables.Email]);
        Assert.Equal("10 minutes", job.Variables[AdminEmailVariables.ExpiryTime]);
        Assert.False(string.IsNullOrWhiteSpace(job.Variables[AdminEmailVariables.Heading]));
        Assert.StartsWith("https://admin.example.test/verify-email?token=", job.Variables[AdminEmailVariables.WebUrl]);
    }

    [Fact]
    public async Task Register_WithAWrongSecret_StoresNothingAndSendsNoEmail()
    {
        var h = new Harness();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.RegisterAsync(Register("a@example.com", "wrong-secret")));

        Assert.Equal(ApiErrorCodes.InvalidSecretCode, exception.ErrorCode);
        Assert.Null(await h.Store.GetPendingAsync("a@example.com"));
        Assert.Empty(h.Emails.Jobs);
        Assert.Contains(h.Audit.Authentication, e =>
            e.EventType == AuditEvents.Auth.AdminSecretFailed && e.Outcome == AuditOutcome.Suspicious);
    }

    [Fact]
    public async Task Register_LocksAnEmailAfterFiveWrongSecrets_EvenForTheRightOne()
    {
        var h = new Harness();

        for (var i = 0; i < AdminAuthService.MaxSecretAttempts; i++)
        {
            var wrong = await Assert.ThrowsAsync<AppException>(() =>
                h.Service.RegisterAsync(Register("a@example.com", "wrong-secret")));
            Assert.Equal(ApiErrorCodes.InvalidSecretCode, wrong.ErrorCode);
        }

        var locked = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.RegisterAsync(Register("a@example.com", Secret)));

        Assert.Equal(ApiErrorCodes.TooManyAttempts, locked.ErrorCode);
        Assert.Empty(h.Emails.Jobs);
    }

    [Fact]
    public async Task Register_WithTheRightSecret_ResetsTheFailureCount()
    {
        var h = new Harness();
        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<AppException>(() =>
                h.Service.RegisterAsync(Register("a@example.com", "wrong-secret")));
        }

        await h.Service.RegisterAsync(Register("a@example.com", Secret));

        Assert.Equal(0, await h.Store.GetSecretFailuresAsync("a@example.com"));
    }

    [Fact]
    public async Task Register_RejectsAnEmailThatAlreadyHasAnAdmin()
    {
        var h = new Harness();
        await h.RegisterAndVerifyAsync("owner@example.com");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.RegisterAsync(Register("owner@example.com", Secret)));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Register_RequiresAllThreeFields()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.RegisterAsync(
            new AdminRegisterRequest { Email = "a@example.com", Password = Password, SecretCode = "" }));
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.RegisterAsync(
            new AdminRegisterRequest { Email = "not-an-email", Password = Password, SecretCode = Secret }));
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.RegisterAsync(
            new AdminRegisterRequest { Email = "a@example.com", Password = "short", SecretCode = Secret }));
        Assert.Empty(h.Emails.Jobs);
    }

    [Fact]
    public async Task VerifyEmail_CreatesTheAdmin_TheLinkWorksOnce_AndTheSamePasswordLogsIn()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Register("a@example.com", Secret));
        var token = h.Emails.LastToken();

        await h.Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = token });

        var admin = Assert.Single(h.Admins.All);
        Assert.True(admin.IsVerified);
        Assert.Equal(AdminStatus.Active, admin.Status);
        Assert.Null(await h.Store.GetPendingAsync("a@example.com"));

        var reuse = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = token }));
        Assert.Equal(ApiErrorCodes.InvalidToken, reuse.ErrorCode);

        var login = await h.Service.LoginAsync(new AdminLoginRequest { Email = "A@example.com", Password = Password });
        Assert.Equal("a@example.com", login.Email);
    }

    [Fact]
    public async Task VerifyEmail_AnExpiredOrUnknownToken_IsRejected()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Register("a@example.com", Secret));
        var token = h.Emails.LastToken();
        h.Store.ExpireTokens();

        var expired = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = token }));
        var unknown = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = "not-a-token" }));

        Assert.Equal(ApiErrorCodes.InvalidToken, expired.ErrorCode);
        Assert.Equal(ApiErrorCodes.InvalidToken, unknown.ErrorCode);
        Assert.Empty(h.Admins.All);
    }

    [Fact]
    public async Task VerifyEmail_AfterThePendingRecordExpired_AsksToRegisterAgain()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Register("a@example.com", Secret));
        var token = h.Emails.LastToken();
        await h.Store.DeletePendingAsync("a@example.com");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            h.Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = token }));

        Assert.Equal(ApiErrorCodes.RegistrationExpired, exception.ErrorCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SendsNothingAndDoesNotThrow()
    {
        var h = new Harness();

        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "ghost@example.com" });

        Assert.Empty(h.Emails.Jobs);
    }

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsAResetLink_AndTheCooldownBlocksAnImmediateSecondOne()
    {
        var h = new Harness();
        await h.RegisterAndVerifyAsync("a@example.com");
        h.Emails.Jobs.Clear();

        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });
        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });

        var job = Assert.Single(h.Emails.Jobs);
        Assert.Equal(AdminEmailTemplate.ForgotPassword, job.Template);
        Assert.StartsWith("https://admin.example.test/reset-password?token=", job.Variables[AdminEmailVariables.WebUrl]);
        Assert.Equal("10 minutes", job.Variables[AdminEmailVariables.ExpiryTime]);
    }

    [Fact]
    public async Task ResetPassword_ChangesThePassword_SignsEverySessionOut_AndTheTokenWorksOnce()
    {
        var h = new Harness();
        var admin = await h.RegisterAndVerifyAsync("a@example.com");
        h.Emails.Jobs.Clear();
        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });
        var token = h.Emails.LastToken();

        await h.Service.ResetPasswordAsync(new AdminResetPasswordRequest
        {
            Token = token,
            NewPassword = "Brand-New-Passw0rd",
            ConfirmPassword = "Brand-New-Passw0rd"
        });

        Assert.Contains(admin.Id, h.Sessions.RevokedAdmins);
        await Assert.ThrowsAsync<AppException>(() =>
            h.Service.LoginAsync(new AdminLoginRequest { Email = "a@example.com", Password = Password }));
        await h.Service.LoginAsync(new AdminLoginRequest { Email = "a@example.com", Password = "Brand-New-Passw0rd" });

        var reuse = await Assert.ThrowsAsync<AppException>(() => h.Service.ResetPasswordAsync(new AdminResetPasswordRequest
        {
            Token = token,
            NewPassword = "Another-Passw0rd",
            ConfirmPassword = "Another-Passw0rd"
        }));
        Assert.Equal(ApiErrorCodes.InvalidToken, reuse.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_ANewRequestInvalidatesTheEarlierLink()
    {
        var h = new Harness();
        await h.RegisterAndVerifyAsync("a@example.com");
        h.Emails.Jobs.Clear();
        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });
        var first = h.Emails.LastToken();
        h.Store.ClearForgotCooldown();
        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });
        var second = h.Emails.LastToken();

        var old = await Assert.ThrowsAsync<AppException>(() => h.Service.ResetPasswordAsync(new AdminResetPasswordRequest
        {
            Token = first,
            NewPassword = "Brand-New-Passw0rd",
            ConfirmPassword = "Brand-New-Passw0rd"
        }));
        await h.Service.ResetPasswordAsync(new AdminResetPasswordRequest
        {
            Token = second,
            NewPassword = "Brand-New-Passw0rd",
            ConfirmPassword = "Brand-New-Passw0rd"
        });

        Assert.Equal(ApiErrorCodes.InvalidToken, old.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_ConfirmationMustMatch()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.ResetPasswordAsync(new AdminResetPasswordRequest
        {
            Token = "token",
            NewPassword = "Brand-New-Passw0rd",
            ConfirmPassword = "Different-Passw0rd"
        }));
    }

    [Fact]
    public async Task ChangePassword_RequiresTheCurrentPassword()
    {
        var h = new Harness();
        var admin = await h.RegisterAndVerifyAsync("a@example.com");

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Service.ChangePasswordAsync(
            admin.Id,
            new AdminChangePasswordRequest
            {
                CurrentPassword = "Wrong-Passw0rd",
                NewPassword = "Brand-New-Passw0rd",
                ConfirmPassword = "Brand-New-Passw0rd"
            }));

        Assert.Equal("currentPassword", exception.Field);
        Assert.Empty(h.Sessions.RevokedAdmins);
    }

    [Fact]
    public async Task ChangePassword_ValidatesTheNewPassword()
    {
        var h = new Harness();
        var admin = await h.RegisterAndVerifyAsync("a@example.com");

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.ChangePasswordAsync(
            admin.Id,
            new AdminChangePasswordRequest { CurrentPassword = Password, NewPassword = Password, ConfirmPassword = Password }));
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.ChangePasswordAsync(
            admin.Id,
            new AdminChangePasswordRequest { CurrentPassword = Password, NewPassword = "Brand-New-Passw0rd", ConfirmPassword = "Mismatch-Passw0rd" }));
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.ChangePasswordAsync(
            admin.Id,
            new AdminChangePasswordRequest { CurrentPassword = Password, NewPassword = "short", ConfirmPassword = "short" }));
    }

    [Fact]
    public async Task ChangePassword_UpdatesThePassword_SignsOutEverySession_AndSendsNoEmail()
    {
        var h = new Harness();
        var admin = await h.RegisterAndVerifyAsync("a@example.com");
        h.Emails.Jobs.Clear();

        await h.Service.ChangePasswordAsync(
            admin.Id,
            new AdminChangePasswordRequest
            {
                CurrentPassword = Password,
                NewPassword = "Brand-New-Passw0rd",
                ConfirmPassword = "Brand-New-Passw0rd"
            });

        Assert.Contains(admin.Id, h.Sessions.RevokedAdmins);
        Assert.Empty(h.Emails.Jobs);
        await h.Service.LoginAsync(new AdminLoginRequest { Email = "a@example.com", Password = "Brand-New-Passw0rd" });
    }

    [Fact]
    public async Task Audit_NeverContainsPasswordsTokensOrTheSecretCode()
    {
        var h = new Harness();
        await h.RegisterAndVerifyAsync("a@example.com");
        await h.Service.ForgotPasswordAsync(new AdminForgotPasswordRequest { Email = "a@example.com" });

        var text = string.Join(
            "|",
            h.Audit.Authentication.Select(e =>
                $"{e.EventType}{e.FailureReason}{e.Subject}{string.Join(",", e.Metadata?.Values ?? [])}"));
        Assert.DoesNotContain(Password, text);
        Assert.DoesNotContain(Secret, text);
        Assert.DoesNotContain(h.Emails.LastToken(), text);
    }


    private static AdminRegisterRequest Register(string email, string secret) => new()
    {
        Email = email,
        Password = Password,
        SecretCode = secret
    };

    private sealed class Harness
    {
        public Harness()
        {
            Service = new AdminAuthService(
                Admins,
                Store,
                new FixedSettings(),
                Emails,
                Sessions,
                new AdminRegisterValidator(),
                new AdminVerifyEmailValidator(),
                new AdminLoginValidator(),
                new AdminForgotPasswordValidator(),
                new AdminResetPasswordValidator(),
                new AdminChangePasswordValidator(),
                Audit);
        }

        public InMemoryAdminRepository Admins { get; } = new();
        public InMemoryAdminAuthStore Store { get; } = new();
        public RecordingAdminEmailQueue Emails { get; } = new();
        public FakeAdminSessions Sessions { get; } = new();
        public RecordingAuditLogWriter Audit { get; } = new();
        public AdminAuthService Service { get; }

        public async Task<Admin> RegisterAndVerifyAsync(string email)
        {
            await Service.RegisterAsync(Register(email, Secret));
            await Service.VerifyEmailAsync(new AdminVerifyEmailRequest { Token = Emails.LastToken() });
            return Admins.All.Single(a => a.Email == email.ToLowerInvariant());
        }
    }

    private sealed class FixedSettings : IAdminAuthSettings
    {
        public string SecretCode => Secret;
        public string WebBaseUrl => "https://admin.example.test";
    }

    private sealed class InMemoryAdminRepository : IAdminRepository
    {
        public List<Admin> All { get; } = [];

        public Task<Admin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(All.FirstOrDefault(a => a.Id == id));

        public Task<Admin?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(All.FirstOrDefault(a => a.Email == email));

        public Task AddAsync(Admin admin, CancellationToken cancellationToken = default)
        {
            All.Add(admin);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAdminSessions : IAdminSessionService
    {
        public List<Guid> RevokedAdmins { get; } = [];

        public Task<TokenResponse> CreateAsync(Guid adminId, string email, CancellationToken cancellationToken = default)
            => Task.FromResult(new TokenResponse { AccessToken = "access", RefreshToken = "refresh" });

        public Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAllAsync(Guid adminId, CancellationToken cancellationToken = default)
        {
            RevokedAdmins.Add(adminId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAdminEmailQueue : IAdminEmailQueue
    {
        public List<AdminEmailJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(AdminEmailJob job, CancellationToken cancellationToken = default)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public string LastToken()
        {
            var url = Jobs.Last().Variables[AdminEmailVariables.WebUrl];
            return Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
        }
    }

    private sealed class InMemoryAdminAuthStore : IAdminAuthStore
    {
        private readonly Dictionary<string, PendingAdminRegistration> _pending = [];
        private readonly Dictionary<string, string> _verify = [];
        private readonly Dictionary<string, Guid> _reset = [];
        private readonly Dictionary<Guid, string> _resetCurrent = [];
        private readonly HashSet<string> _cooldown = [];
        private readonly Dictionary<string, int> _failures = [];

        public void ExpireTokens()
        {
            _verify.Clear();
            _reset.Clear();
        }

        public void ClearForgotCooldown() => _cooldown.Clear();

        public Task<PendingAdminRegistration?> GetPendingAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_pending.GetValueOrDefault(email));

        public Task SetPendingAsync(string email, PendingAdminRegistration pending, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _pending[email] = pending;
            return Task.CompletedTask;
        }

        public Task DeletePendingAsync(string email, CancellationToken cancellationToken = default)
        {
            _pending.Remove(email);
            return Task.CompletedTask;
        }

        public Task<int> GetSecretFailuresAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult(_failures.GetValueOrDefault(email));

        public Task RecordSecretFailureAsync(string email, TimeSpan window, CancellationToken cancellationToken = default)
        {
            _failures[email] = _failures.GetValueOrDefault(email) + 1;
            return Task.CompletedTask;
        }

        public Task ClearSecretFailuresAsync(string email, CancellationToken cancellationToken = default)
        {
            _failures.Remove(email);
            return Task.CompletedTask;
        }

        public Task SetVerificationTokenAsync(string tokenHash, string email, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            _verify[tokenHash] = email;
            return Task.CompletedTask;
        }

        public Task<string?> ConsumeVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            var found = _verify.Remove(tokenHash, out var email);
            return Task.FromResult(found ? email : null);
        }

        public Task SetResetTokenAsync(Guid adminId, string tokenHash, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            if (_resetCurrent.TryGetValue(adminId, out var previous))
            {
                _reset.Remove(previous);
            }

            _reset[tokenHash] = adminId;
            _resetCurrent[adminId] = tokenHash;
            return Task.CompletedTask;
        }

        public Task<Guid?> ConsumeResetTokenAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            if (!_reset.Remove(tokenHash, out var adminId))
            {
                return Task.FromResult<Guid?>(null);
            }

            _resetCurrent.Remove(adminId);
            return Task.FromResult<Guid?>(adminId);
        }

        public Task<bool> TryStartForgotPasswordCooldownAsync(string email, TimeSpan cooldown, CancellationToken cancellationToken = default)
            => Task.FromResult(_cooldown.Add(email));
    }
}
