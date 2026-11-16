using FluentValidation;
using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Validators
{
    public class RequestOtpValidator : AbstractValidator<RequestOtpRequest>
    {
        public RequestOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .Cascade(CascadeMode.Stop)
                .Must(phoneNumber => PhoneNumberNormalizer.TryNormalize(phoneNumber, out _))
                .WithMessage("Phone number must be in E.164 format.")
                .Must(IsDeliverable)
                .WithMessage("Phone number must be a valid Indian mobile number.");
        }

        /// <summary>The OTP goes out by SMS through an India-only provider, so only Indian mobile numbers can sign up or in.</summary>
        private static bool IsDeliverable(string phoneNumber)
        {
            return PhoneNumberNormalizer.TryNormalize(phoneNumber, out var normalized)
                && IndianMobileNumber.IsValid(normalized);
        }
    }
}
