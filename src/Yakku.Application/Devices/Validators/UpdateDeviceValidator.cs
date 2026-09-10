using FluentValidation;
using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Devices.Validators
{
    public class UpdateDeviceValidator : AbstractValidator<UpdateDeviceRequest>
    {
        public UpdateDeviceValidator()
        {
            RuleFor(x => x.PushToken)
                .MaximumLength(4096)
                .When(x => x.PushToken is not null);

            RuleFor(x => x.Platform)
                .Must(value => DeviceFieldParser.TryParsePlatform(value, out _))
                .When(x => !string.IsNullOrWhiteSpace(x.Platform))
                .WithMessage("Platform must be android or ios.");

            RuleFor(x => x.DeviceModel)
                .MaximumLength(150)
                .When(x => x.DeviceModel is not null);

            RuleFor(x => x.OsVersion)
                .MaximumLength(50)
                .When(x => x.OsVersion is not null);

            RuleFor(x => x.AppVersion)
                .MaximumLength(50)
                .When(x => x.AppVersion is not null);

            RuleFor(x => x.AppBuild)
                .MaximumLength(50)
                .When(x => x.AppBuild is not null);

            RuleFor(x => x.Locale)
                .MaximumLength(20)
                .When(x => x.Locale is not null);

            RuleFor(x => x.Timezone)
                .MaximumLength(100)
                .When(x => x.Timezone is not null);

            RuleFor(x => x.NotificationPermission)
                .Must(value => DeviceFieldParser.TryParseNotificationPermission(value, out _))
                .When(x => !string.IsNullOrWhiteSpace(x.NotificationPermission))
                .WithMessage("Notification permission must be unknown, granted, or denied.");
        }
    }
}
