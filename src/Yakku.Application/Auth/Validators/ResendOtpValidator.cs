using FluentValidation;
using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Validators
{
    public class ResendOtpValidator : AbstractValidator<ResendOtpRequest>
    {
        public ResendOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .Must(phoneNumber => PhoneNumberNormalizer.TryNormalize(phoneNumber, out _))
                .WithMessage("Phone number must be in E.164 format.");
        }
    }
}
