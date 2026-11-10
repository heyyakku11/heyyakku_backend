using FluentValidation;
using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Validators
{
    public class UpdateUserSettingsValidator : AbstractValidator<UpdateUserSettingsRequest>
    {
        public UpdateUserSettingsValidator()
        {
            RuleFor(x => x)
                .Must(x => x.AllowAskBack is not null)
                .WithMessage("At least one setting is required.")
                .OverridePropertyName("settings");
        }
    }
}
