using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.Auth;

namespace Yakku.Application.AdminAuth.Validators
{
    public class AdminVerifyOtpValidator : AbstractValidator<AdminVerifyOtpRequest>
    {
        public AdminVerifyOtpValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256)
                .Must(AdminEmailPolicy.IsAllowed)
                .WithMessage("Only @heyyakku.com email addresses can register.");

            RuleFor(x => x.Otp)
                .NotEmpty()
                .Length(OtpOptions.Length)
                .Matches(@"^\d{6}$")
                .WithMessage("OTP must be a 6-digit code.");
        }
    }
}
