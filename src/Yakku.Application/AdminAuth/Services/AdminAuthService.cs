using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Services
{
    public class AdminAuthService : IAdminAuthService
    {
        private readonly IAdminRepository _adminRepository;
        private readonly IAdminOtpChallengeStore _otpChallengeStore;
        private readonly IOtpGenerator _otpGenerator;
        private readonly IEmailLogRepository _emailLogRepository;
        private readonly IOtpEmailQueue _otpEmailQueue;
        private readonly IAdminSessionService _sessionService;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<AdminRegisterRequest> _registerValidator;
        private readonly IValidator<AdminVerifyOtpRequest> _verifyOtpValidator;
        private readonly IValidator<AdminLoginRequest> _loginValidator;

        public AdminAuthService(
            IAdminRepository adminRepository,
            IAdminOtpChallengeStore otpChallengeStore,
            IOtpGenerator otpGenerator,
            IEmailLogRepository emailLogRepository,
            IOtpEmailQueue otpEmailQueue,
            IAdminSessionService sessionService,
            ISystemLogWriter systemLogWriter,
            IValidator<AdminRegisterRequest> registerValidator,
            IValidator<AdminVerifyOtpRequest> verifyOtpValidator,
            IValidator<AdminLoginRequest> loginValidator)
        {
            _adminRepository = adminRepository;
            _otpChallengeStore = otpChallengeStore;
            _otpGenerator = otpGenerator;
            _emailLogRepository = emailLogRepository;
            _otpEmailQueue = otpEmailQueue;
            _sessionService = sessionService;
            _systemLogWriter = systemLogWriter;
            _registerValidator = registerValidator;
            _verifyOtpValidator = verifyOtpValidator;
            _loginValidator = loginValidator;
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
                OtpHash = OtpHasher.Hash(email, otp),
                Purpose = OtpPurpose.Registration,
                PasswordHash = AdminPasswordHasher.Hash(request.Password),
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpChallengeStore.SetAsync(email, challenge, OtpOptions.Ttl, cancellationToken);

            var emailLog = new EmailLog(
                email,
                EmailType.Otp,
                challengeId,
                OtpEmailQueueOptions.ProviderName,
                OtpEmailQueueOptions.MaxAttempts);

            await _emailLogRepository.AddAsync(emailLog, cancellationToken);
            await _emailLogRepository.SaveChangesAsync(cancellationToken);

            await _otpEmailQueue.EnqueueAsync(
                new EmailJob(emailLog.Id, challengeId, email, otp),
                cancellationToken);

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.OtpRequested,
                    Message = "Admin OTP requested.",
                    Details = new
                    {
                        email,
                        purpose = OtpPurpose.Registration.ToString(),
                        emailLogId = emailLog.Id,
                        challengeId
                    }
                },
                cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = OtpPurpose.Registration.ToString(),
                ExpiresInSeconds = OtpOptions.ExpiresInSeconds
            };
        }

        public async Task<TokenResponse> VerifyOtpAsync(
            AdminVerifyOtpRequest request,
            CancellationToken cancellationToken = default)
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
                await LogOtpInvalidAsync(email, ApiErrorCodes.OtpAttemptsExceeded, cancellationToken);
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpAttemptsExceeded,
                    "Too many invalid OTP attempts. Request a new OTP.",
                    "otp");
            }

            if (!OtpHasher.Verify(email, request.Otp.Trim(), challenge.OtpHash))
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
                    await LogOtpInvalidAsync(email, ApiErrorCodes.OtpAttemptsExceeded, cancellationToken);
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpAttemptsExceeded,
                        "Too many invalid OTP attempts. Request a new OTP.",
                        "otp");
                }

                await LogOtpInvalidAsync(email, ApiErrorCodes.OtpInvalid, cancellationToken);
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

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.AdminRegistered,
                    Message = "Admin registered.",
                    Details = new { email, adminId = admin.Id }
                },
                cancellationToken);

            return await _sessionService.CreateAsync(admin.Id, admin.Email, cancellationToken);
        }

        public async Task<TokenResponse> LoginAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default)
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

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.AdminLoggedIn,
                    Message = "Admin logged in.",
                    Details = new { email, adminId = admin.Id }
                },
                cancellationToken);

            return await _sessionService.CreateAsync(admin.Id, admin.Email, cancellationToken);
        }

        private Task LogOtpInvalidAsync(string email, string reason, CancellationToken cancellationToken)
        {
            return _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Warning,
                    EventType = SystemLogEventTypes.OtpInvalid,
                    Message = "Admin OTP verification failed.",
                    Details = new { email, reason }
                },
                cancellationToken);
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
