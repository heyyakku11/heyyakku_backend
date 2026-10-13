using FluentValidation;
using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Validators
{
    public class RequestOtpValidator : AbstractValidator<RequestOtpRequest>
    {
        public RequestOtpValidator()
        {
            RuleFor(x => x.PhoneNumber)
                .Must(phoneNumber => PhoneNumberNormalizer.TryNormalize(phoneNumber, out _))
                .WithMessage("Phone number must be in E.164 format.");

            RuleFor(x => x.Name)
                .MaximumLength(100)
                .When(x => !string.IsNullOrWhiteSpace(x.Name));
        }
    }
}
