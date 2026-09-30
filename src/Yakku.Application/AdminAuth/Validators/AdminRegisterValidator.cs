using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;

namespace Yakku.Application.AdminAuth.Validators
{
    public class AdminRegisterValidator : AbstractValidator<AdminRegisterRequest>
    {
        public AdminRegisterValidator()
        {
            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .MaximumLength(256)
                .Must(AdminEmailPolicy.IsAllowed)
                .WithMessage("Only @heyyakku.com email addresses can register.");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(128);
        }
    }
}
