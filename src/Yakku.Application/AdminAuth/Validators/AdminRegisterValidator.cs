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
                .MaximumLength(256);

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(128);

            RuleFor(x => x.SecretCode)
                .NotEmpty()
                .MaximumLength(256);
        }
    }
}
