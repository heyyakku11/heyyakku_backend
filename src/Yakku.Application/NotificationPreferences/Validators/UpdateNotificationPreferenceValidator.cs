using FluentValidation;
using Yakku.Application.NotificationPreferences.DTOs;

namespace Yakku.Application.NotificationPreferences.Validators
{
    public class UpdateNotificationPreferenceValidator : AbstractValidator<UpdateNotificationPreferenceRequest>
    {
        public UpdateNotificationPreferenceValidator()
        {
            RuleFor(x => x)
                .Must(HasAnyField)
                .WithMessage("At least one preference field must be provided.");
        }

        private static bool HasAnyField(UpdateNotificationPreferenceRequest request)
        {
            return request.PushEnabled.HasValue
                || request.PollActivityEnabled.HasValue
                || request.OffersEnabled.HasValue
                || request.AlertsEnabled.HasValue
                || request.NormalEnabled.HasValue;
        }
    }
}
