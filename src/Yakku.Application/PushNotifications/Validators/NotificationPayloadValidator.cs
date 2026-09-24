using FluentValidation;
using Yakku.Application.PushNotifications.DTOs;

namespace Yakku.Application.PushNotifications.Validators
{
    public class NotificationPayloadValidator : AbstractValidator<NotificationPayload>
    {
        public NotificationPayloadValidator()
        {
            RuleFor(x => x.Title)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(200);

            RuleFor(x => x.Body)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(2000);

            RuleFor(x => x.Data)
                .Must(data => data is null || data.Keys.All(key => !string.IsNullOrWhiteSpace(key)))
                .WithMessage("Data keys must be non-empty strings.")
                .When(x => x.Data is not null);

            RuleFor(x => x.Data)
                .Must(data => data is null || data.Values.All(value => value is not null))
                .WithMessage("Data values must not be null.")
                .When(x => x.Data is not null);
        }
    }
}
