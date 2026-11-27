using FluentValidation;
using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Validators
{
    public class UpdateUserPermissionsValidator : AbstractValidator<UpdateUserPermissionsRequest>
    {
        public UpdateUserPermissionsValidator()
        {
            RuleFor(x => x)
                .Must(x => x.AllowAskBack is not null
                           || x.PushNotification is not null
                           || x.Sms is not null
                           || x.WhatsApp is not null)
                .WithMessage("At least one permission is required.")
                .OverridePropertyName("permissions");
        }
    }
}
