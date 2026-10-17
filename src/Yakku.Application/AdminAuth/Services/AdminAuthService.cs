using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Services
{
    public class AdminAuthService : IAdminAuthService
    {
        /// <summary>How long the temporary registration record lives.</summary>
        public static readonly TimeSpan PendingRegistrationTtl = TimeSpan.FromMinutes(15);

        /// <summary>How long the email link tokens (verify, reset) live.</summary>
        public static readonly TimeSpan LinkTokenTtl = TimeSpan.FromMinutes(10);

        public const string LinkTokenExpiryText = "10 minutes";
        public const int MaxSecretAttempts = 5;
        public static readonly TimeSpan ForgotPasswordCooldown = TimeSpan.FromSeconds(60);

        private readonly IAdminRepository _adminRepository;
        private readonly IAdminAuthStore _store;
        private readonly IAdminAuthSettings _settings;
        private readonly IAdminEmailQueue _emailQueue;
        private readonly IAdminSessionService _sessionService;
        private readonly IValidator<AdminRegisterRequest> _registerValidator;
        private readonly IValidator<AdminConfirmSecretRequest> _confirmSecretValidator;
        private readonly IValidator<AdminVerifyEmailRequest> _verifyEmailValidator;
        private readonly IValidator<AdminLoginRequest> _loginValidator;
        private readonly IValidator<AdminForgotPasswordRequest> _forgotValidator;
        private readonly IValidator<AdminResetPasswordRequest> _resetValidator;
        private readonly IValidator<AdminChangePasswordRequest> _changeValidator;
        private readonly IAuditLogWriter _audit;

        public AdminAuthService(
            IAdminRepository adminRepository,
            IAdminAuthStore store,
            IAdminAuthSettings settings,
            IAdminEmailQueue emailQueue,
            IAdminSessionService sessionService,
            IValidator<AdminRegisterRequest> registerValidator,
            IValidator<AdminConfirmSecretRequest> confirmSecretValidator,
            IValidator<AdminVerifyEmailRequest> verifyEmailValidator,
            IValidator<AdminLoginRequest> loginValidator,
            IValidator<AdminForgotPasswordRequest> forgotValidator,
            IValidator<AdminResetPasswordRequest> resetValidator,
            IValidator<AdminChangePasswordRequest> changeValidator,
            IAuditLogWriter audit)
        {
            _adminRepository = adminRepository;
            _store = store;
            _settings = settings;
            _emailQueue = emailQueue;
            _sessionService = sessionService;
            _registerValidator = registerValidator;
            _confirmSecretValidator = confirmSecretValidator;
            _verifyEmailValidator = verifyEmailValidator;
            _loginValidator = loginValidator;
            _forgotValidator = forgotValidator;
            _resetValidator = resetValidator;
            _changeValidator = changeValidator;
            _audit = audit;
        }

        public async Task<AdminRegistrationStepResponse> RegisterAsync(
            AdminRegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            var admin = await _adminRepository.GetByEmailAsync(email, cancellationToken);
            if (admin is { IsVerified: true })
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An admin with this email already exists.",
                    "email");
            }

            // Registering again replaces the earlier pending record and restarts the 15 minutes.
            await _store.SetPendingAsync(
                email,
                new PendingAdminRegistration
                {
                    Email = email,
                    PasswordHash = AdminPasswordHasher.Hash(request.Password),
                    CreatedAt = DateTime.UtcNow
                },
                PendingRegistrationTtl,
                cancellationToken);

            await AuditAsync(AuditEvents.Auth.AdminRegistrationStarted, AuditOutcome.Success, null, email);

            return new AdminRegistrationStepResponse { ExpiresInSeconds = (int)PendingRegistrationTtl.TotalSeconds };
        }

        public async Task<AdminRegistrationStepResponse> ConfirmSecretAsync(
            AdminConfirmSecretRequest request,
            CancellationToken cancellationToken = default)
        {
            await _confirmSecretValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            var pending = await _store.GetPendingAsync(email, cancellationToken)
                ?? throw RegistrationExpired();

            if (pending.AttemptCount >= MaxSecretAttempts)
            {
                await _store.DeletePendingAsync(email, cancellationToken);
                await AuditAsync(AuditEvents.Auth.AdminSecretFailed, AuditOutcome.Suspicious, ApiErrorCodes.TooManyAttempts, email);
                throw TooManyAttempts();
            }

            // Both checks always run so a failure does not reveal which one was wrong.
            var passwordMatches = AdminPasswordHasher.Verify(request.Password, pending.PasswordHash);
            var secretMatches = SecretMatches(request.SecretCode, _settings.SecretCode);
            if (!passwordMatches || !secretMatches)
            {
                pending.AttemptCount++;
                if (pending.AttemptCount >= MaxSecretAttempts)
                {
                    await _store.DeletePendingAsync(email, cancellationToken);
                }
                else
                {
                    await _store.ReplacePendingKeepingTtlAsync(email, pending, cancellationToken);
                }

                await AuditAsync(AuditEvents.Auth.AdminSecretFailed, AuditOutcome.Suspicious, ApiErrorCodes.InvalidSecretCode, email);
                throw new AppException(
                    400,
                    ApiErrorCodes.InvalidSecretCode,
                    "Invalid email, password or secret code.");
            }

            if (await _adminRepository.GetByEmailAsync(email, cancellationToken) is { IsVerified: true })
            {
                await _store.DeletePendingAsync(email, cancellationToken);
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An admin with this email already exists.",
                    "email");
            }

            var token = NewToken();
            await _store.SetVerificationTokenAsync(HashToken(token), email, LinkTokenTtl, cancellationToken);

            pending.SecretConfirmed = true;
            await _store.ReplacePendingKeepingTtlAsync(email, pending, cancellationToken);

            await EnqueueEmailAsync(
                AdminEmailTemplate.Registration,
                email,
                "Verify your Yakku admin email",
                $"{_settings.WebBaseUrl}/verify-email?token={Uri.EscapeDataString(token)}",
                cancellationToken);

            await AuditAsync(AuditEvents.Auth.AdminSecretConfirmed, AuditOutcome.Success, null, email);

            return new AdminRegistrationStepResponse { ExpiresInSeconds = (int)LinkTokenTtl.TotalSeconds };
        }

        public async Task VerifyEmailAsync(
            AdminVerifyEmailRequest request,
            CancellationToken cancellationToken = default)
        {
            await _verifyEmailValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = await _store.ConsumeVerificationTokenAsync(HashToken(request.Token.Trim()), cancellationToken)
                ?? throw InvalidToken();

            var pending = await _store.GetPendingAsync(email, cancellationToken);
            if (pending is null || !pending.SecretConfirmed)
            {
                throw RegistrationExpired();
            }

            if (await _adminRepository.GetByEmailAsync(email, cancellationToken) is not null)
            {
                await _store.DeletePendingAsync(email, cancellationToken);
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An admin with this email already exists.",
                    "email");
            }

            var admin = new Admin(email, pending.PasswordHash, AdminRole.SuperAdmin);
            admin.MarkVerified();
            await _adminRepository.AddAsync(admin, cancellationToken);
            await _adminRepository.SaveChangesAsync(cancellationToken);
            await _store.DeletePendingAsync(email, cancellationToken);

            await AuditAsync(AuditEvents.Auth.AdminEmailVerified, AuditOutcome.Success, null, email, admin.Id);
        }

        public async Task<AdminAuthResponse> LoginAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await LoginCoreAsync(request, cancellationToken);
                await AuditAsync(AuditEvents.Auth.LoggedIn, AuditOutcome.Success, null, request.Email, metadata: "password");
                return response;
            }
            catch (AppException exception)
            {
                // Admin accounts are high value: bad passwords are flagged as suspicious.
                var outcome = exception.ErrorCode == ApiErrorCodes.Unauthorized
                    ? AuditOutcome.Suspicious
                    : AuditOutcome.Failure;
                await AuditAsync(AuditEvents.Auth.LoginFailed, outcome, exception.ErrorCode, request.Email);
                throw;
            }
        }

        private async Task<AdminAuthResponse> LoginCoreAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken)
        {
            await _loginValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            var admin = await _adminRepository.GetByEmailAsync(email, cancellationToken);
            if (admin is null || !AdminPasswordHasher.Verify(request.Password, admin.PasswordHash))
            {
                throw new AppException(
                    401,
                    ApiErrorCodes.Unauthorized,
                    "Invalid email or password.",
                    "email");
            }

            if (!admin.IsVerified || admin.Status == AdminStatus.Pending)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "This admin account is not verified.",
                    "email");
            }

            if (admin.Status == AdminStatus.Disabled)
            {
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "This admin account is disabled.",
                    "email");
            }

            admin.RecordLogin();
            await _adminRepository.SaveChangesAsync(cancellationToken);

            return ToAuthResponse(
                await _sessionService.CreateAsync(admin.Id, admin.Email, cancellationToken),
                admin);
        }

        public async Task ForgotPasswordAsync(
            AdminForgotPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            await _forgotValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);

            // Every outcome below looks the same to the caller so the route cannot be used to find admin emails.
            if (!await _store.TryStartForgotPasswordCooldownAsync(email, ForgotPasswordCooldown, cancellationToken))
            {
                return;
            }

            var admin = await _adminRepository.GetByEmailAsync(email, cancellationToken);
            if (admin is not { IsVerified: true, Status: AdminStatus.Active })
            {
                return;
            }

            var token = NewToken();
            await _store.SetResetTokenAsync(admin.Id, HashToken(token), LinkTokenTtl, cancellationToken);

            await EnqueueEmailAsync(
                AdminEmailTemplate.ForgotPassword,
                admin.Email,
                "Reset your Yakku admin password",
                $"{_settings.WebBaseUrl}/reset-password?token={Uri.EscapeDataString(token)}",
                cancellationToken);

            await AuditAsync(AuditEvents.Auth.PasswordResetRequested, AuditOutcome.Success, null, email, admin.Id);
        }

        public async Task ResetPasswordAsync(
            AdminResetPasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            await _resetValidator.ValidateAndThrowAsync(request, cancellationToken);

            var adminId = await _store.ConsumeResetTokenAsync(HashToken(request.Token.Trim()), cancellationToken);
            var admin = adminId is null
                ? null
                : await _adminRepository.GetByIdAsync(adminId.Value, cancellationToken);
            if (admin is not { IsVerified: true, Status: AdminStatus.Active })
            {
                await AuditAsync(AuditEvents.Auth.PasswordResetFailed, AuditOutcome.Failure, ApiErrorCodes.InvalidToken, null);
                throw InvalidToken();
            }

            admin.UpdatePassword(AdminPasswordHasher.Hash(request.NewPassword));
            await _adminRepository.SaveChangesAsync(cancellationToken);
            await _sessionService.RevokeAllAsync(admin.Id, cancellationToken);

            await AuditAsync(AuditEvents.Auth.PasswordResetCompleted, AuditOutcome.Success, null, admin.Email, admin.Id);
        }

        public async Task ChangePasswordAsync(
            Guid adminId,
            AdminChangePasswordRequest request,
            CancellationToken cancellationToken = default)
        {
            await _changeValidator.ValidateAndThrowAsync(request, cancellationToken);

            var admin = await _adminRepository.GetByIdAsync(adminId, cancellationToken);
            if (admin is not { IsVerified: true, Status: AdminStatus.Active })
            {
                throw new AppException(401, ApiErrorCodes.Unauthorized, "Admin not found.");
            }

            if (!AdminPasswordHasher.Verify(request.CurrentPassword, admin.PasswordHash))
            {
                await AuditAsync(AuditEvents.Auth.PasswordChangeFailed, AuditOutcome.Suspicious, ApiErrorCodes.ValidationError, admin.Email, admin.Id);
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Current password is incorrect.",
                    "currentPassword");
            }

            admin.UpdatePassword(AdminPasswordHasher.Hash(request.NewPassword));
            await _adminRepository.SaveChangesAsync(cancellationToken);

            // Every session ends, so the admin signs in again with the new password.
            await _sessionService.RevokeAllAsync(admin.Id, cancellationToken);

            await AuditAsync(AuditEvents.Auth.PasswordChanged, AuditOutcome.Success, null, admin.Email, admin.Id);
        }

        private ValueTask EnqueueEmailAsync(
            AdminEmailTemplate template,
            string email,
            string heading,
            string webUrl,
            CancellationToken cancellationToken)
        {
            return _emailQueue.EnqueueAsync(
                new AdminEmailJob(
                    Guid.NewGuid(),
                    template,
                    email,
                    new Dictionary<string, string>
                    {
                        [AdminEmailVariables.Heading] = heading,
                        [AdminEmailVariables.Email] = email,
                        [AdminEmailVariables.WebUrl] = webUrl,
                        [AdminEmailVariables.ExpiryTime] = LinkTokenExpiryText
                    }),
                cancellationToken);
        }

        private Task AuditAsync(
            string eventType,
            AuditOutcome outcome,
            string? failureReason,
            string? email,
            Guid? adminId = null,
            string? metadata = null)
        {
            // Passwords, tokens and the secret code are never written to the audit log.
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    eventType,
                    outcome,
                    FailureReason: failureReason,
                    Subject: email,
                    Metadata: metadata is null ? null : new Dictionary<string, string?> { ["method"] = metadata }),
                CancellationToken.None);
        }

        private static AdminAuthResponse ToAuthResponse(TokenResponse tokens, Admin admin)
        {
            return new AdminAuthResponse
            {
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresInSeconds = tokens.AccessTokenExpiresInSeconds,
                RefreshTokenExpiresInSeconds = tokens.RefreshTokenExpiresInSeconds,
                Email = admin.Email,
                Role = admin.Role.ToString()
            };
        }

        private static bool SecretMatches(string provided, string expected)
        {
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }

        private static string NewToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        private static string HashToken(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }

        private static AppException RegistrationExpired()
        {
            return new AppException(
                404,
                ApiErrorCodes.RegistrationExpired,
                "This registration has expired. Please register again.");
        }

        private static AppException TooManyAttempts()
        {
            return new AppException(
                400,
                ApiErrorCodes.TooManyAttempts,
                "Too many invalid attempts. Please register again.");
        }

        private static AppException InvalidToken()
        {
            return new AppException(
                400,
                ApiErrorCodes.InvalidToken,
                "This link is invalid or has expired.",
                "token");
        }
    }
}
