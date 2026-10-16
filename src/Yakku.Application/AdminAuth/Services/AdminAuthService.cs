using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
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
        private readonly IAdminRepository _adminRepository;
        private readonly IAdminOtpChallengeStore _otpChallengeStore;
        private readonly IOtpGenerator _otpGenerator;
        private readonly IOtpEmailQueue _otpEmailQueue;
        private readonly IAdminSessionService _sessionService;
        private readonly IValidator<AdminRegisterRequest> _registerValidator;
        private readonly IValidator<AdminVerifyOtpRequest> _verifyOtpValidator;
        private readonly IValidator<AdminLoginRequest> _loginValidator;
        private readonly IAuditLogWriter _audit;

        public AdminAuthService(
            IAdminRepository adminRepository,
            IAdminOtpChallengeStore otpChallengeStore,
            IOtpGenerator otpGenerator,
            IOtpEmailQueue otpEmailQueue,
            IAdminSessionService sessionService,
            IValidator<AdminRegisterRequest> registerValidator,
            IValidator<AdminVerifyOtpRequest> verifyOtpValidator,
            IValidator<AdminLoginRequest> loginValidator,
            IAuditLogWriter audit)
        {
            _adminRepository = adminRepository;
            _otpChallengeStore = otpChallengeStore;
            _otpGenerator = otpGenerator;
            _otpEmailQueue = otpEmailQueue;
            _sessionService = sessionService;
            _registerValidator = registerValidator;
            _verifyOtpValidator = verifyOtpValidator;
            _loginValidator = loginValidator;
            _audit = audit;
        }

        public async Task<RequestOtpResponse> RegisterAsync(
            AdminRegisterRequest request,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            EnsureAllowedEmail(email);

            var admin = await _adminRepository.GetByEmailAsync(email, cancellationToken);
            if (admin is { IsVerified: true })
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An admin with this email already exists.",
                    "email");
            }

            var existing = await _otpChallengeStore.GetAsync(email, cancellationToken);
            if (existing is not null && DateTime.UtcNow - existing.CreatedAt < OtpOptions.ResendCooldown)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpResendCooldown,
                    "Please wait before requesting another OTP.",
                    "email");
            }

            var otp = _otpGenerator.Generate();
            var challengeId = Guid.NewGuid();
            var challenge = new OtpChallenge
            {
                ChallengeId = challengeId,
                // OtpHash = OtpHasher.Hash(email, otp),
                OtpHash = otp,
                Purpose = OtpPurpose.Registration,
                PasswordHash = AdminPasswordHasher.Hash(request.Password),
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpChallengeStore.SetAsync(email, challenge, OtpOptions.Ttl, cancellationToken);

            await _otpEmailQueue.EnqueueAsync(
                new EmailJob(challengeId, email, otp),
                cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = OtpPurpose.Registration.ToString(),
                ExpiresInSeconds = OtpOptions.ExpiresInSeconds
            };
        }

        public async Task<AdminAuthResponse> VerifyOtpAsync(
            AdminVerifyOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await VerifyOtpCoreAsync(request, cancellationToken);
                await AuditSuccessAsync(AuditEvents.Auth.LoggedIn, request.Email, "otp");
                return response;
            }
            catch (AppException exception)
            {
                await AuditFailureAsync(AuditEvents.Auth.OtpVerifyFailed, exception, request.Email);
                throw;
            }
        }

        private async Task<AdminAuthResponse> VerifyOtpCoreAsync(
            AdminVerifyOtpRequest request,
            CancellationToken cancellationToken)
        {
            await _verifyOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            EnsureAllowedEmail(email);

            var challenge = await _otpChallengeStore.GetAsync(email, cancellationToken);
            if (challenge is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.OtpNotFound,
                    "OTP not found or has expired.",
                    "otp");
            }

            if (challenge.AttemptCount >= OtpOptions.MaxVerificationAttempts)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpAttemptsExceeded,
                    "Too many invalid OTP attempts. Request a new OTP.",
                    "otp");
            }

            // if (!OtpHasher.Verify(email, request.Otp.Trim(), challenge.OtpHash))
            if (!string.Equals(request.Otp.Trim(), challenge.OtpHash, StringComparison.Ordinal))
            {
                challenge.AttemptCount++;
                var replaced = await _otpChallengeStore.ReplaceKeepingTtlAsync(email, challenge, cancellationToken);
                if (!replaced)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpExpired,
                        "OTP has expired.",
                        "otp");
                }

                if (challenge.AttemptCount >= OtpOptions.MaxVerificationAttempts)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpAttemptsExceeded,
                        "Too many invalid OTP attempts. Request a new OTP.",
                        "otp");
                }

                throw new AppException(
                    400,
                    ApiErrorCodes.OtpInvalid,
                    "Invalid OTP.",
                    "otp");
            }

            var admin = await _adminRepository.GetByEmailAsync(email, cancellationToken);
            if (admin is { Status: AdminStatus.Disabled })
            {
                await _otpChallengeStore.DeleteAsync(email, cancellationToken);
                throw new AppException(
                    403,
                    ApiErrorCodes.Forbidden,
                    "This admin account is disabled.",
                    "email");
            }

            if (admin is null)
            {
                if (string.IsNullOrWhiteSpace(challenge.PasswordHash))
                {
                    await _otpChallengeStore.DeleteAsync(email, cancellationToken);
                    throw new AppException(
                        404,
                        ApiErrorCodes.NotFound,
                        "Admin not found.",
                        "email");
                }

                admin = new Admin(email, challenge.PasswordHash, AdminRole.SuperAdmin);
                await _adminRepository.AddAsync(admin, cancellationToken);
            }
            else if (!admin.IsVerified && !string.IsNullOrWhiteSpace(challenge.PasswordHash))
            {
                admin.UpdatePassword(challenge.PasswordHash);
            }

            admin.MarkVerified();
            admin.RecordLogin();
            await _adminRepository.SaveChangesAsync(cancellationToken);
            await _otpChallengeStore.DeleteAsync(email, cancellationToken);

            return ToAuthResponse(
                await _sessionService.CreateAsync(admin.Id, admin.Email, cancellationToken),
                admin);
        }

        public async Task<AdminAuthResponse> LoginAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await LoginCoreAsync(request, cancellationToken);
                await AuditSuccessAsync(AuditEvents.Auth.LoggedIn, request.Email, "password");
                return response;
            }
            catch (AppException exception)
            {
                await AuditFailureAsync(AuditEvents.Auth.LoginFailed, exception, request.Email);
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

        private Task AuditSuccessAsync(string eventType, string? email, string method)
        {
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    null,
                    eventType,
                    AuditOutcome.Success,
                    Subject: email,
                    Metadata: new Dictionary<string, string?> { ["method"] = method }),
                CancellationToken.None);
        }

        private Task AuditFailureAsync(string eventType, AppException exception, string? email)
        {
            // Admin accounts are high value: exhausted OTP attempts and bad passwords are flagged as suspicious.
            var outcome = exception.ErrorCode is ApiErrorCodes.OtpAttemptsExceeded
                || (exception.ErrorCode == ApiErrorCodes.Unauthorized && eventType == AuditEvents.Auth.LoginFailed)
                ? AuditOutcome.Suspicious
                : AuditOutcome.Failure;

            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    null,
                    eventType,
                    outcome,
                    FailureReason: exception.ErrorCode,
                    Subject: email),
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

        private static void EnsureAllowedEmail(string email)
        {
            if (!AdminEmailPolicy.IsAllowed(email))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Only @heyyakku.com email addresses can register.",
                    "email");
            }
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
