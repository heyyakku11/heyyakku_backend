using FluentValidation;
using Yakku.Application.Devices;
using Yakku.Application.PushNotifications.DTOs;

namespace Yakku.Application.PushNotifications.Validators
{
    public class TestDeviceNotificationRequestValidator : AbstractValidator<TestDeviceNotificationRequest>
    {
        public TestDeviceNotificationRequestValidator()
        {
            RuleFor(x => x.InstallationId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.Platform)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(value => DeviceFieldParser.TryParsePlatform(value, out _))
                .WithMessage("Platform must be android or ios.");

            RuleFor(x => x.Notification)
                .NotNull()
                .SetValidator(new NotificationPayloadValidator());
        }
    }
}
