using FluentValidation;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;

namespace Yakku.Application.System.Services
{
    public class SystemService : ISystemService
    {
        private readonly IOtpChallengeStore _otpChallengeStore;
        private readonly IValidator<DecryptOtpRequest> _decryptOtpValidator;

        public SystemService(
            IOtpChallengeStore otpChallengeStore,
            IValidator<DecryptOtpRequest> decryptOtpValidator)
        {
            _otpChallengeStore = otpChallengeStore;
            _decryptOtpValidator = decryptOtpValidator;
        }

        public async Task<DecryptOtpResponse> DecryptOtpAsync(
            DecryptOtpRequest request,
            CancellationToken cancellationToken = default)
        {
            await _decryptOtpValidator.ValidateAndThrowAsync(request, cancellationToken);

            var email = request.Email.Trim().ToLowerInvariant();
            var otpHash = string.IsNullOrWhiteSpace(request.OtpHash)
                ? null
                : request.OtpHash.Trim();

            OtpChallenge? challenge = null;
            if (otpHash is null)
            {
                challenge = await _otpChallengeStore.GetAsync(email, cancellationToken);
                if (challenge is null)
                {
                    throw new AppException(
                        404,
                        ApiErrorCodes.OtpNotFound,
                        "OTP not found or has expired.",
                        "email");
                }

                otpHash = challenge.OtpHash;
            }

            var otp = OtpHasher.TryRecover(email, otpHash);
            if (otp is null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.OtpInvalid,
                    "OTP hash could not be decrypted.",
                    "otpHash");
            }

            int? expiresInSeconds = null;
            if (challenge is not null)
            {
                var remaining = OtpOptions.Ttl - (DateTime.UtcNow - challenge.CreatedAt);
                expiresInSeconds = Math.Max(0, (int)remaining.TotalSeconds);
            }

            return new DecryptOtpResponse
            {
                Email = email,
                Otp = otp,
                OtpHash = otpHash,
                Purpose = challenge?.Purpose.ToString(),
                AttemptCount = challenge?.AttemptCount,
                CreatedAt = challenge?.CreatedAt,
                ExpiresInSeconds = expiresInSeconds
            };
        }
    }
}
