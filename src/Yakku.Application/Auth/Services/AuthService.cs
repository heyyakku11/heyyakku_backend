using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class AuthService : IAuthService
    {
        private const int AnonymousNameRetryLimit = 8;

        private readonly IUserRepository _userRepository;
        private readonly IOtpChallengeStore _otpChallengeStore;
        private readonly IOtpGenerator _otpGenerator;
        private readonly IDisplayNameGenerator _displayNameGenerator;
        private readonly ISessionService _sessionService;
        private readonly IValidator<RequestOtpRequest> _requestOtpValidator;
        private readonly IValidator<ResendOtpRequest> _resendOtpValidator;
        private readonly IValidator<VerifyOtpRequest> _verifyOtpValidator;
        private readonly IAuditLogWriter _audit;

        public AuthService(
            IUserRepository userRepository,
            IOtpChallengeStore otpChallengeStore,
            IOtpGenerator otpGenerator,
            IDisplayNameGenerator displayNameGenerator,
            ISessionService sessionService,
            IValidator<RequestOtpRequest> requestOtpValidator,
            IValidator<ResendOtpRequest> resendOtpValidator,
            IValidator<VerifyOtpRequest> verifyOtpValidator,
            IAuditLogWriter audit)
        {
            _userRepository = userRepository;
            _otpChallengeStore = otpChallengeStore;
            _otpGenerator = otpGenerator;
            _displayNameGenerator = displayNameGenerator;
            _sessionService = sessionService;
            _requestOtpValidator = requestOtpValidator;
            _resendOtpValidator = resendOtpValidator;
            _verifyOtpValidator = verifyOtpValidator;
            _audit = audit;
        }

        public async Task<RequestOtpResponse> RequestOtpAsync(
            RequestOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await RequestOtpCoreAsync(request, cancellationToken);
                await _audit.WriteAuthenticationAsync(
                    new AuthenticationAuditEntry(
                        AuditActorType.User,
                        null,
                        AuditEvents.Auth.OtpRequested,
                        AuditOutcome.Success,
                        Subject: request.PhoneNumber,
                        Metadata: new Dictionary<string, string?> { ["purpose"] = response.Purpose }),
                    CancellationToken.None);
                return response;
            }
            catch (AppException exception)
            {
                await AuditFailureAsync(AuditEvents.Auth.OtpRequested, exception, request.PhoneNumber);
                throw;
            }
        }

        private async Task<RequestOtpResponse> RequestOtpCoreAsync(
            RequestOtpRequest request,
            CancellationToken cancellationToken)
        {
            await _requestOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
            var purpose = user is null ? OtpPurpose.Registration : OtpPurpose.Login;

            if (purpose == OtpPurpose.Registration && string.IsNullOrWhiteSpace(request.Name))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Name is required.",
                    "name");
            }

            var existing = await _otpChallengeStore.GetAsync(phoneNumber, cancellationToken);
            if (existing is not null && DateTime.UtcNow - existing.CreatedAt < OtpOptions.ResendCooldown)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpResendCooldown,
                    "Please wait before requesting another OTP.",
                    "phoneNumber");
            }

            var otp = _otpGenerator.Generate();
            var challengeId = Guid.NewGuid();
            string? anonymousName = null;
            string? name = null;
            if (purpose == OtpPurpose.Registration)
            {
                name = request.Name!.Trim();
                anonymousName = existing?.AnonymousName ?? await CreateUniqueAnonymousNameAsync(cancellationToken);
            }

            var challenge = new OtpChallenge
            {
                ChallengeId = challengeId,
                // OtpHash = OtpHasher.Hash(phoneNumber, otp),
                OtpHash = otp,
                Purpose = purpose,
                Name = name,
                AnonymousName = anonymousName,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpChallengeStore.SetAsync(phoneNumber, challenge, OtpOptions.Ttl, cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = purpose.ToString(),
                ExpiresInSeconds = OtpOptions.ExpiresInSeconds
            };
        }

        public async Task<RequestOtpResponse> ResendOtpAsync(
            ResendOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            await _resendOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
            var existing = await _otpChallengeStore.GetAsync(phoneNumber, cancellationToken);
            if (existing is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.OtpNotFound,
                    "OTP not found or has expired.",
                    "phoneNumber");
            }

            if (DateTime.UtcNow - existing.CreatedAt < OtpOptions.ResendCooldown)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpResendCooldown,
                    "Please wait before requesting another OTP.",
                    "phoneNumber");
            }

            var otp = _otpGenerator.Generate();
            var challengeId = Guid.NewGuid();
            var challenge = new OtpChallenge
            {
                ChallengeId = challengeId,
                OtpHash = otp,
                Purpose = existing.Purpose,
                Name = existing.Name,
                AnonymousName = existing.AnonymousName,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpChallengeStore.SetAsync(phoneNumber, challenge, OtpOptions.Ttl, cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = existing.Purpose.ToString(),
                ExpiresInSeconds = OtpOptions.ExpiresInSeconds
            };
        }

        public async Task<VerifyOtpResponse> VerifyOtpAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await VerifyOtpCoreAsync(request, cancellationToken);
                await _audit.WriteAuthenticationAsync(
                    new AuthenticationAuditEntry(
                        AuditActorType.User,
                        response.UserId,
                        response.Purpose == nameof(OtpPurpose.Registration)
                            ? AuditEvents.Auth.Registered
                            : AuditEvents.Auth.LoggedIn,
                        AuditOutcome.Success,
                        Subject: request.PhoneNumber),
                    CancellationToken.None);
                return response;
            }
            catch (AppException exception)
            {
                await AuditFailureAsync(AuditEvents.Auth.OtpVerifyFailed, exception, request.PhoneNumber);
                throw;
            }
        }

        private async Task<VerifyOtpResponse> VerifyOtpCoreAsync(
            VerifyOtpRequest request,
            CancellationToken cancellationToken)
        {
            await _verifyOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var phoneNumber = NormalizePhoneNumber(request.PhoneNumber);
            var challenge = await _otpChallengeStore.GetAsync(phoneNumber, cancellationToken);
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

            // if (!OtpHasher.Verify(phoneNumber, request.Otp.Trim(), challenge.OtpHash))
            if (!string.Equals(request.Otp.Trim(), challenge.OtpHash, StringComparison.Ordinal))
            {
                challenge.AttemptCount++;
                var replaced = await _otpChallengeStore.ReplaceKeepingTtlAsync(phoneNumber, challenge, cancellationToken);
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

            if (challenge.Purpose == OtpPurpose.Registration)
            {
                return await CompleteRegistrationAsync(phoneNumber, challenge, request.Device, cancellationToken);
            }

            return await CompleteLoginAsync(phoneNumber, request.Device, cancellationToken);
        }

        private async Task<VerifyOtpResponse> CompleteRegistrationAsync(
            string phoneNumber,
            OtpChallenge challenge,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var name = challenge.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Name is required.",
                    "name");
            }

            var anonymousName = string.IsNullOrWhiteSpace(challenge.AnonymousName)
                ? await CreateUniqueAnonymousNameAsync(cancellationToken)
                : challenge.AnonymousName;

            var user = new User(phoneNumber, name, anonymousName);
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
            await _otpChallengeStore.DeleteAsync(phoneNumber, cancellationToken);

            return await ToResponseAsync(user, OtpPurpose.Registration, installation, cancellationToken);
        }

        private async Task<VerifyOtpResponse> CompleteLoginAsync(
            string phoneNumber,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
            if (user is null)
            {
                await _otpChallengeStore.DeleteAsync(phoneNumber, cancellationToken);
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "User not found.",
                    "phoneNumber");
            }

            // Logging in again inside the grace period discards the scheduled account deletion.
            var restored = user.CancelDeletion();
            user.RecordLogin();
            await _userRepository.SaveChangesAsync(cancellationToken);
            await _otpChallengeStore.DeleteAsync(phoneNumber, cancellationToken);

            if (restored)
            {
                await _audit.WriteAuthenticationAsync(
                    new AuthenticationAuditEntry(
                        AuditActorType.User,
                        user.Id,
                        AuditEvents.Auth.AccountDeletionCancelled,
                        AuditOutcome.Success,
                        Subject: phoneNumber),
                    CancellationToken.None);
            }

            var response = await ToResponseAsync(user, OtpPurpose.Login, installation, cancellationToken);
            response.AccountRestored = restored;
            return response;
        }

        private async Task<string> CreateUniqueAnonymousNameAsync(CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < AnonymousNameRetryLimit; attempt++)
            {
                var anonymousName = _displayNameGenerator.Generate();
                if (!await _userRepository.AnonymousNameExistsAsync(anonymousName, cancellationToken))
                {
                    return anonymousName;
                }
            }

            throw new AppException(
                409,
                ApiErrorCodes.Conflict,
                "Anonymous name already exists.",
                "anonymousName");
        }

        private async Task<VerifyOtpResponse> ToResponseAsync(
            User user,
            OtpPurpose purpose,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var tokens = await _sessionService.CreateAsync(user.Id, installation, cancellationToken);

            return new VerifyOtpResponse
            {
                UserId = user.Id,
                Name = user.Profile?.Name ?? string.Empty,
                AnonymousName = user.Profile?.AnonymousName ?? string.Empty,
                Purpose = purpose.ToString(),
                AccessToken = tokens.AccessToken,
                RefreshToken = tokens.RefreshToken,
                AccessTokenExpiresInSeconds = tokens.AccessTokenExpiresInSeconds,
                RefreshTokenExpiresInSeconds = tokens.RefreshTokenExpiresInSeconds
            };
        }

        private Task AuditFailureAsync(string eventType, AppException exception, string? subject)
        {
            // Exhausting OTP attempts is the brute-force signal; everything else is an ordinary failure.
            var outcome = exception.ErrorCode == ApiErrorCodes.OtpAttemptsExceeded
                ? AuditOutcome.Suspicious
                : AuditOutcome.Failure;

            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.User,
                    null,
                    eventType,
                    outcome,
                    FailureReason: exception.ErrorCode,
                    Subject: subject),
                CancellationToken.None);
        }

        private static string NormalizePhoneNumber(string phoneNumber)
        {
            if (!PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalized))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Phone number must be in E.164 format.",
                    "phoneNumber");
            }

            return normalized;
        }
    }
}
