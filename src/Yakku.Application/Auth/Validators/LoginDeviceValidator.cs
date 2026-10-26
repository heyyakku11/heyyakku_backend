using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Devices;
using Yakku.Application.Devices.Validators;

namespace Yakku.Application.Auth.Validators
{
    public class LoginDeviceValidator : AbstractValidator<LoginDeviceRequest>
    {
        public LoginDeviceValidator()
        {
            RuleFor(x => x.InstallationId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .MaximumLength(100)
                .PrintableWithoutSpaces();

            RuleFor(x => x.Platform)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                .Must(value => DeviceFieldParser.TryParsePlatform(value, out _))
                .WithMessage("Platform must be android or ios.");
        }
    }
}
