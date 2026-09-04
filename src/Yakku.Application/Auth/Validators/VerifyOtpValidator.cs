using FluentValidation;
using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Validators
{
    public class VerifyOtpValidator : AbstractValidator<VerifyOtpRequest>
    {
        public VerifyOtpValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256);

            RuleFor(x => x.Otp)
                .NotEmpty()
                .Length(OtpOptions.Length)
                .Matches(@"^\d{6}$")
                .WithMessage("OTP must be a 6-digit code.");
        }
    }
}
