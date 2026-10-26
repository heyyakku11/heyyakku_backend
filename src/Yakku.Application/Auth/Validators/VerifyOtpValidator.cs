using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Devices.Validators;

namespace Yakku.Application.Auth.Validators
{
    public class VerifyOtpValidator : AbstractValidator<VerifyOtpRequest>
    {
        public VerifyOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .Must(phoneNumber => PhoneNumberNormalizer.TryNormalize(phoneNumber, out _))
                .WithMessage("Phone number must be in E.164 format.");

            RuleFor(x => x.Otp)
                .NotEmpty()
                .Length(OtpOptions.Length)
                .Matches(@"^\d{6}$")
                .WithMessage("OTP must be a 6-digit code.");

            RuleFor(x => x.Device!)
                .SetValidator(new RegisterDeviceValidator())
                .When(x => x.Device is not null);
        }
    }
}
