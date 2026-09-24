using FluentValidation;
using Yakku.Application.PushNotifications.DTOs;

namespace Yakku.Application.PushNotifications.Validators
{
    public class TestUserNotificationRequestValidator : AbstractValidator<TestUserNotificationRequest>
    {
        public TestUserNotificationRequestValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty();

            RuleFor(x => x.Notification)
                .NotNull()
                .SetValidator(new NotificationPayloadValidator());
        }
    }
}
