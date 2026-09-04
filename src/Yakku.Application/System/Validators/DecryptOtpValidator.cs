using FluentValidation;
using Yakku.Application.System.DTOs;

namespace Yakku.Application.System.Validators
{
    public class DecryptOtpValidator : AbstractValidator<DecryptOtpRequest>
    {
        public DecryptOtpValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);

            RuleFor(x => x.OtpHash)
                .MaximumLength(128)
                .When(x => !string.IsNullOrWhiteSpace(x.OtpHash));
        }
    }
}
