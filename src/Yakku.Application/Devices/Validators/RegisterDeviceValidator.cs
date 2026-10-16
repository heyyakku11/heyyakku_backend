using FluentValidation;
using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Devices.Validators
{
    public class RegisterDeviceValidator : AbstractValidator<RegisterDeviceRequest>
    {
        public RegisterDeviceValidator()
        {
            RuleFor(x => x.InstallationId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(100)
                .PrintableWithoutSpaces();

            RuleFor(x => x.PushToken)
                .MaximumLength(4096)
                .PrintableWithoutSpaces()
                .When(x => x.PushToken is not null);

            RuleFor(x => x.Platform)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(value => DeviceFieldParser.TryParsePlatform(value, out _))
                .WithMessage("Platform must be android or ios.");

            RuleFor(x => x.DeviceModel).MaximumLength(150).NoControlCharacters();
            RuleFor(x => x.OsVersion).MaximumLength(50).NoControlCharacters();
            RuleFor(x => x.AppVersion).MaximumLength(50).NoControlCharacters();
            RuleFor(x => x.AppBuild).MaximumLength(50).NoControlCharacters();
            RuleFor(x => x.Locale).MaximumLength(20).NoControlCharacters();
            RuleFor(x => x.Timezone).MaximumLength(100).NoControlCharacters();

            RuleFor(x => x.NotificationPermission)
                .Must(value => DeviceFieldParser.TryParseNotificationPermission(value, out _))
                .When(x => !string.IsNullOrWhiteSpace(x.NotificationPermission))
                .WithMessage("Notification permission must be unknown, granted, or denied.");
        }
    }
}
