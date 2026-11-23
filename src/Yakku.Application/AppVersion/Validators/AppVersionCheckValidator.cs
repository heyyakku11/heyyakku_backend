using FluentValidation;
using Yakku.Application.AppVersion.DTOs;
using Yakku.Application.Devices;

namespace Yakku.Application.AppVersion.Validators
{
    public class AppVersionCheckValidator : AbstractValidator<AppVersionCheckRequest>
    {
        public AppVersionCheckValidator()
        {
            RuleFor(x => x.Platform)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(value => DeviceFieldParser.TryParsePlatform(value, out _))
                .WithMessage("Platform must be android or ios.");

            RuleFor(x => x.Version)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(value => SemVersion.TryParse(value, out _))
                .WithMessage("Version must be a semver value such as 1.2.3.");
        }
    }
}
