using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class AuthService : IAuthService
    {
        private const int DisplayNameRetryLimit = 8;

        private readonly IUserRepository _userRepository;
        private readonly IOtpChallengeStore _otpChallengeStore;
        private readonly IOtpGenerator _otpGenerator;
        private readonly IDisplayNameGenerator _displayNameGenerator;
        private readonly IEmailSender _emailSender;
        private readonly ISessionService _sessionService;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<RequestOtpRequest> _requestOtpValidator;
        private readonly IValidator<VerifyOtpRequest> _verifyOtpValidator;

        public AuthService(
            IUserRepository userRepository,
            IOtpChallengeStore otpChallengeStore,
            IOtpGenerator otpGenerator,
            IDisplayNameGenerator displayNameGenerator,
            IEmailSender emailSender,
            ISessionService sessionService,
            ISystemLogWriter systemLogWriter,
            IValidator<RequestOtpRequest> requestOtpValidator,
            IValidator<VerifyOtpRequest> verifyOtpValidator)
        {
            _userRepository = userRepository;
            _otpChallengeStore = otpChallengeStore;
            _otpGenerator = otpGenerator;
            _displayNameGenerator = displayNameGenerator;
            _emailSender = emailSender;
            _sessionService = sessionService;
            _systemLogWriter = systemLogWriter;
            _requestOtpValidator = requestOtpValidator;
            _verifyOtpValidator = verifyOtpValidator;
        }

        public async Task<RequestOtpResponse> RequestOtpAsync(
            RequestOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            await _requestOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            var purpose = user is null ? OtpPurpose.Registration : OtpPurpose.Login;

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
            var displayName = purpose == OtpPurpose.Registration
                ? existing?.DisplayName ?? await CreateUniqueDisplayNameAsync(cancellationToken)
                : null;

            var challenge = new OtpChallenge
            {
                OtpHash = OtpHasher.Hash(email, otp),
                Purpose = purpose,
                DisplayName = displayName,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpChallengeStore.SetAsync(email, challenge, OtpOptions.Ttl, cancellationToken);
            await _emailSender.SendOtpAsync(email, otp, cancellationToken);
            await LogAsync(
                SystemLogLevel.Information,
                SystemLogEventTypes.OtpRequested,
                "OTP requested.",
                new { email, purpose = purpose.ToString() },
                user?.Id,
                cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = purpose.ToString(),
                ExpiresInSeconds = OtpOptions.ExpiresInSeconds
            };
        }

        public async Task<VerifyOtpResponse> VerifyOtpAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            await _verifyOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = NormalizeEmail(request.Email);
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

            if (challenge.Purpose == OtpPurpose.Registration)
            {
                return await CompleteRegistrationAsync(email, challenge, cancellationToken);
            }

            return await CompleteLoginAsync(email, cancellationToken);
        }

        private async Task<VerifyOtpResponse> CompleteRegistrationAsync(
            string email,
            OtpChallenge challenge,
            CancellationToken cancellationToken)
        {
            var displayName = string.IsNullOrWhiteSpace(challenge.DisplayName)
                ? await CreateUniqueDisplayNameAsync(cancellationToken)
                : challenge.DisplayName;

            var user = new User(email, displayName);
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            await _otpChallengeStore.DeleteAsync(email, cancellationToken);
            await LogAsync(
                SystemLogLevel.Information,
                SystemLogEventTypes.UserRegistered,
                "User registered.",
                new { email },
                user.Id,
                cancellationToken);

            return await ToResponseAsync(user, OtpPurpose.Registration, cancellationToken);
        }

        private async Task<VerifyOtpResponse> CompleteLoginAsync(
            string email,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            if (user is null)
            {
                await _otpChallengeStore.DeleteAsync(email, cancellationToken);
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "User not found.",
                    "email");
            }

            user.RecordLogin();
            await _userRepository.SaveChangesAsync(cancellationToken);
            await _otpChallengeStore.DeleteAsync(email, cancellationToken);
            await LogAsync(
                SystemLogLevel.Information,
                SystemLogEventTypes.UserLoggedIn,
                "User logged in.",
                new { email },
                user.Id,
                cancellationToken);

            return await ToResponseAsync(user, OtpPurpose.Login, cancellationToken);
        }

        private async Task<string> CreateUniqueDisplayNameAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < DisplayNameRetryLimit; attempt++)
            {
                var displayName = _displayNameGenerator.Generate();
                if (!await _userRepository.DisplayNameExistsAsync(displayName, cancellationToken))
                {
                    return displayName;
                }
            }

            throw new AppException(
                409,
                ApiErrorCodes.Conflict,
                "Display name already exists.",
                "displayName");
        }

        private async Task<VerifyOtpResponse> ToResponseAsync(
            User user,
            OtpPurpose purpose,
            CancellationToken cancellationToken)
        {
            var tokens = await _sessionService.CreateAsync(user.Id, cancellationToken);

            return new VerifyOtpResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.Profile?.DisplayName ?? string.Empty,
                Purpose = purpose.ToString(),
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresInSeconds = tokens.AccessTokenExpiresInSeconds,
                RefreshTokenExpiresInSeconds = tokens.RefreshTokenExpiresInSeconds
            };
        }

        private Task LogOtpInvalidAsync(string email, string reason, CancellationToken cancellationToken)
        {
            return LogAsync(
                SystemLogLevel.Warning,
                SystemLogEventTypes.OtpInvalid,
                "OTP verification failed.",
                new { email, reason },
                userId: null,
                cancellationToken);
        }

        private Task LogAsync(
            SystemLogLevel level,
            string eventType,
            string message,
            object? details,
            Guid? userId,
            CancellationToken cancellationToken)
        {
            return _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = level,
                    EventType = eventType,
                    Message = message,
                    Details = details,
                    UserId = userId
                },
                cancellationToken);
        }

        private static string NormalizeEmail(string email)
        {
            return email.Trim().ToLowerInvariant();
        }
    }
}
