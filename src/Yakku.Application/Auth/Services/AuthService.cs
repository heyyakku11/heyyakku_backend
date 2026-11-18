using FluentValidation;
using Microsoft.Extensions.Logging;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Auth.Sms;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class AuthService : IAuthService
    {
        private const string DeliveryFailedMessage = "We couldn't send the verification code right now. Please try again shortly.";

        private readonly IUserRepository _userRepository;
        private readonly IOtpChallengeStore _otpChallengeStore;
        private readonly IOtpGenerator _otpGenerator;
        private readonly IAnonymousNameAllocator _anonymousNames;
        private readonly ISessionService _sessionService;
        private readonly IValidator<RequestOtpRequest> _requestOtpValidator;
        private readonly IValidator<ResendOtpRequest> _resendOtpValidator;
        private readonly IValidator<VerifyOtpRequest> _verifyOtpValidator;
        private readonly IAuditLogWriter _audit;
        private readonly ILoginSnapshotReader _loginSnapshot;
        private readonly ISmsService _sms;
        private readonly OtpPolicy _policy;
        private readonly ILogger<AuthService> _logger;

        /// <summary>A reserved name outlives its OTP challenge slightly, then frees itself if unclaimed.</summary>
        private readonly TimeSpan _nameHold;

        public AuthService(
            IUserRepository userRepository,
            IOtpChallengeStore otpChallengeStore,
            IOtpGenerator otpGenerator,
            IAnonymousNameAllocator anonymousNames,
            ISessionService sessionService,
            IValidator<RequestOtpRequest> requestOtpValidator,
            IValidator<ResendOtpRequest> resendOtpValidator,
            IValidator<VerifyOtpRequest> verifyOtpValidator,
            IAuditLogWriter audit,
            ILoginSnapshotReader loginSnapshot,
            ISmsService sms,
            OtpPolicy policy,
            ILogger<AuthService> logger)
        {
            _sms = sms;
            _policy = policy;
            _logger = logger;
            _nameHold = policy.Ttl + TimeSpan.FromMinutes(1);
            _userRepository = userRepository;
            _otpChallengeStore = otpChallengeStore;
            _otpGenerator = otpGenerator;
            _anonymousNames = anonymousNames;
            _sessionService = sessionService;
            _requestOtpValidator = requestOtpValidator;
            _resendOtpValidator = resendOtpValidator;
            _verifyOtpValidator = verifyOtpValidator;
            _audit = audit;
            _loginSnapshot = loginSnapshot;
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

            // Only read to keep a registering number's reserved name across re-requests. The resend cooldown and
            // the send limit are enforced atomically when the new challenge is stored.
            var existing = await _otpChallengeStore.GetAsync(phoneNumber, cancellationToken);
            string? anonymousName = null;
            if (purpose == OtpPurpose.Registration)
            {
                anonymousName = !string.IsNullOrWhiteSpace(existing?.AnonymousName)
                    ? existing!.AnonymousName
                    : await _anonymousNames.ReserveAsync(_nameHold, cancellationToken);
            }

            await IssueAndSendOtpAsync(phoneNumber, purpose, anonymousName, cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = purpose.ToString(),
                ExpiresInSeconds = _policy.ExpiresInSeconds
            };
        }

        /// <summary>
        /// Generate, store (atomically, with the cooldown and send limit), then deliver. The OTP only counts as sent
        /// once the SMS provider has accepted it; if it did not, the stored OTP is invalidated so nobody is left with
        /// a code that was never delivered. Redis and the provider cannot share a transaction, so this is compensation
        /// by id: it removes only this request's challenge and never a newer one from a concurrent request.
        /// </summary>
        private async Task IssueAndSendOtpAsync(
            string phoneNumber,
            OtpPurpose purpose,
            string? anonymousName,
            CancellationToken cancellationToken)
        {
            var otp = _otpGenerator.Generate();
            // When SMS is off (Development), store plaintext so the code can be read from Redis.
            // When SMS is on, store only the hash.
            var challenge = new OtpChallenge
            {
                ChallengeId = Guid.NewGuid(),
                OtpHash = _sms.IsEnabled ? OtpHasher.Hash(phoneNumber, otp) : otp,
                Purpose = purpose,
                AnonymousName = anonymousName,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            var issued = await _otpChallengeStore.TryIssueAsync(phoneNumber, challenge, _policy, cancellationToken);
            switch (issued.Status)
            {
                case OtpIssueStatus.CooldownActive:
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpResendCooldown,
                        "Please wait before requesting another OTP.",
                        "phoneNumber");
                case OtpIssueStatus.SendLimitReached:
                    throw new AppException(
                        429,
                        ApiErrorCodes.RateLimited,
                        "Too many OTP requests. Please try again later.",
                        "phoneNumber");
            }

            if (!string.IsNullOrWhiteSpace(anonymousName))
            {
                await _anonymousNames.RefreshAsync(anonymousName, _nameHold, cancellationToken);
            }

            if (!_sms.IsEnabled)
            {
                _logger.LogInformation(
                    "SMS delivery disabled; OTP for {PhoneNumber} stored in plaintext in Redis (challenge {ChallengeId}).",
                    phoneNumber,
                    challenge.ChallengeId);
                return;
            }

            SmsSendResult result;
            try
            {
                result = await _sms.SendOtpAsync(
                    new SmsOtpMessage(phoneNumber, otp, challenge.ChallengeId),
                    cancellationToken);
            }
            catch
            {
                // Includes the caller cancelling mid-send: the OTP is stored but its delivery is unknown.
                await RevokeUndeliveredAsync(phoneNumber, challenge.ChallengeId);
                throw;
            }

            await _audit.WriteNotificationAsync(
                new NotificationAuditEntry(
                    NotificationChannel.Sms,
                    "msg91",
                    AuditEvents.Notification.OtpSms,
                    null,
                    null,
                    phoneNumber,
                    challenge.ChallengeId,
                    1,
                    false,
                    result.Accepted ? NotificationAuditStatus.Accepted : NotificationAuditStatus.Failed,
                    result.ProviderRequestId,
                    result.Accepted ? null : result.Failure.ToString(),
                    null,
                    result.LatencyMs),
                CancellationToken.None);

            if (!result.Accepted)
            {
                await RevokeUndeliveredAsync(phoneNumber, challenge.ChallengeId);
                throw ToDeliveryFailure(result.Failure);
            }
        }

        private async Task RevokeUndeliveredAsync(string phoneNumber, Guid challengeId)
        {
            try
            {
                var revoked = await _otpChallengeStore.RevokeUndeliveredAsync(phoneNumber, challengeId, CancellationToken.None);
                _logger.LogInformation(
                    "Undelivered OTP challenge {ChallengeId} {Outcome}.",
                    challengeId,
                    revoked ? "was invalidated" : "was already replaced or expired");
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                // Redis is unreachable. The OTP still expires on its own TTL; the delivery error is what the caller gets.
                _logger.LogWarning(
                    "Could not invalidate undelivered OTP challenge {ChallengeId} ({ExceptionType}); it expires on its own.",
                    challengeId,
                    exception.GetType().Name);
            }
        }

        /// <summary>The client learns that delivery failed, never why: provider details stay in the logs.</summary>
        private static AppException ToDeliveryFailure(SmsFailureCategory failure)
        {
            if (failure == SmsFailureCategory.InvalidRecipient)
            {
                return new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Phone number must be a valid Indian mobile number.",
                    "phoneNumber");
            }

            return new AppException(503, ApiErrorCodes.ServiceUnavailable, DeliveryFailedMessage);
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

            await IssueAndSendOtpAsync(phoneNumber, existing.Purpose, existing.AnonymousName, cancellationToken);

            return new RequestOtpResponse
            {
                Purpose = existing.Purpose.ToString(),
                ExpiresInSeconds = _policy.ExpiresInSeconds
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

            // One atomic step: check the attempt limit, compare, count a miss, and consume on a match. A correct OTP
            // therefore works exactly once, and parallel guesses all count towards the limit.
            // When SMS is disabled the challenge stores plaintext; when enabled it stores a hash.
            var submitted = request.Otp.Trim();
            var comparisonValue = _sms.IsEnabled
                ? OtpHasher.Hash(phoneNumber, submitted)
                : submitted;
            var verification = await _otpChallengeStore.VerifyAndConsumeAsync(
                phoneNumber,
                comparisonValue,
                _policy.MaxVerificationAttempts,
                cancellationToken);

            switch (verification.Status)
            {
                case OtpVerificationStatus.NotFound:
                    throw new AppException(
                        404,
                        ApiErrorCodes.OtpNotFound,
                        "OTP not found or has expired.",
                        "otp");
                case OtpVerificationStatus.AttemptsExceeded:
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpAttemptsExceeded,
                        "Too many invalid OTP attempts. Request a new OTP.",
                        "otp");
                case OtpVerificationStatus.Invalid:
                    throw new AppException(
                        400,
                        ApiErrorCodes.OtpInvalid,
                        "Invalid OTP.",
                        "otp");
            }

            var challenge = verification.Challenge!;
            if (challenge.Purpose == OtpPurpose.Registration)
            {
                return await CompleteRegistrationAsync(phoneNumber, challenge, ToInstallation(request.Device), cancellationToken);
            }

            return await CompleteLoginAsync(phoneNumber, ToInstallation(request.Device), cancellationToken);
        }

        private async Task<VerifyOtpResponse> CompleteRegistrationAsync(
            string phoneNumber,
            OtpChallenge challenge,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var anonymousName = string.IsNullOrWhiteSpace(challenge.AnonymousName)
                ? await _anonymousNames.ReserveAsync(_nameHold, cancellationToken)
                : challenge.AnonymousName;

            User user;
            for (var attempt = 1; ; attempt++)
            {
                user = new User(phoneNumber, anonymousName);
                await _userRepository.AddAsync(user, cancellationToken);
                try
                {
                    await _userRepository.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (AppException exception) when (
                    exception.StatusCode == 409
                    && exception.Field == "anonymousName"
                    && attempt < OperationalSettings.AnonymousNames.MaxAttempts)
                {
                    // Someone took the name between the check and the insert: try another.
                    _userRepository.ClearTracking();
                    await _anonymousNames.ReleaseAsync(anonymousName, CancellationToken.None);
                    anonymousName = await _anonymousNames.ReserveAsync(_nameHold, cancellationToken);
                }
                catch (AppException exception) when (
                    exception.StatusCode == 409
                    && exception.Field == "personalityShareCode"
                    && attempt < OperationalSettings.AnonymousNames.MaxAttempts)
                {
                    // The new personality card's random share code collided: retry with a fresh user and code.
                    // The anonymous name is still ours, so it is kept.
                    _userRepository.ClearTracking();
                }
            }

            // The OTP was consumed when it was verified; deleting the key here could remove a newer OTP.
            await _anonymousNames.ReleaseAsync(anonymousName, CancellationToken.None);

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

        private async Task<VerifyOtpResponse> ToResponseAsync(
            User user,
            OtpPurpose purpose,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var tokens = await _sessionService.CreateAsync(user.Id, installation, cancellationToken);
            var snapshot = await _loginSnapshot.GetAsync(user.Id, installation?.InstallationId, cancellationToken);

            return new VerifyOtpResponse
            {
                UserId = user.Id,
                Purpose = purpose.ToString(),
                User = new VerifyOtpUser
                {
                    AnonymousName = user.Profile?.AnonymousName ?? string.Empty,
                    PersonalityCard = new VerifyOtpPersonalityCard
                    {
                        ShareCode = snapshot.CardShareCode,
                        Status = snapshot.CardStatus
                    },
                    Permissions = new VerifyOtpPermissions
                    {
                        Notification = snapshot.NotificationPermission,
                        IsPushAllowed = snapshot.IsPushAllowed
                    },
                    CommunityNotifyRequested = snapshot.CommunityNotifyRequested
                },
                Auth = tokens
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

        /// <summary>The login payload carries only the installation id and platform; nothing else is assumed.</summary>
        private static RegisterDeviceRequest? ToInstallation(LoginDeviceRequest? device)
        {
            return device is null
                ? null
                : new RegisterDeviceRequest { InstallationId = device.InstallationId, Platform = device.Platform };
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
