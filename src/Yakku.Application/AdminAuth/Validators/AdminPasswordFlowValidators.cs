using FluentValidation;
using Yakku.Application.AdminAuth.DTOs;

namespace Yakku.Application.AdminAuth.Validators
{
    internal static class AdminValidationRules
    {
        public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule)
        {
            return rule.NotEmpty().EmailAddress().MaximumLength(256);
        }

        public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule)
        {
            return rule.NotEmpty().MinimumLength(8).MaximumLength(128);
        }

        public static IRuleBuilderOptions<T, string> ValidToken<T>(this IRuleBuilder<T, string> rule)
        {
            return rule.NotEmpty().MaximumLength(200);
        }
    }

    public class AdminConfirmSecretValidator : AbstractValidator<AdminConfirmSecretRequest>
    {
        public AdminConfirmSecretValidator()
        {
            RuleFor(x => x.Email).ValidEmail();
            RuleFor(x => x.Password).ValidPassword();
            RuleFor(x => x.SecretCode).NotEmpty().MaximumLength(256);
        }
    }

    public class AdminVerifyEmailValidator : AbstractValidator<AdminVerifyEmailRequest>
    {
        public AdminVerifyEmailValidator()
        {
            RuleFor(x => x.Token).ValidToken();
        }
    }

    public class AdminForgotPasswordValidator : AbstractValidator<AdminForgotPasswordRequest>
    {
        public AdminForgotPasswordValidator()
        {
            RuleFor(x => x.Email).ValidEmail();
        }
    }

    public class AdminResetPasswordValidator : AbstractValidator<AdminResetPasswordRequest>
    {
        public AdminResetPasswordValidator()
        {
            RuleFor(x => x.Token).ValidToken();
            RuleFor(x => x.NewPassword).ValidPassword();
            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword)
                .WithMessage("Confirm password must match the new password.");
        }
    }

    public class AdminChangePasswordValidator : AbstractValidator<AdminChangePasswordRequest>
    {
        public AdminChangePasswordValidator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
            RuleFor(x => x.NewPassword)
                .ValidPassword()
                .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must be different from the current password.");
            RuleFor(x => x.ConfirmPassword)
                .Equal(x => x.NewPassword)
                .WithMessage("Confirm password must match the new password.");
        }
    }
}
