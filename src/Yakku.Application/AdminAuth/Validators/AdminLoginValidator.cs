using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;

namespace Yakku.Application.AdminAuth.Validators
{
    public class AdminLoginValidator : AbstractValidator<AdminLoginRequest>
    {
        public AdminLoginValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256)
                .Must(AdminEmailPolicy.IsAllowed)
                .WithMessage("Only @heyyakku.com email addresses can log in.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(128);
        }
    }
}
