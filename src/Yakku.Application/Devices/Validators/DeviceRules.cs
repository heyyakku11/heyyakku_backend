using FluentValidation;

namespace Yakku.Application.Devices.Validators
{
    /// <summary>Shared rules. All device metadata comes from the client and is treated as untrusted text.</summary>
    internal static class DeviceRules
    {
        public static IRuleBuilderOptions<T, string?> NoControlCharacters<T>(this IRuleBuilder<T, string?> rule)
        {
            return rule
                .Must(value => value is null || !value.Any(char.IsControl))
                .WithMessage("'{PropertyName}' must not contain control characters.");
        }

        public static IRuleBuilderOptions<T, string?> PrintableWithoutSpaces<T>(this IRuleBuilder<T, string?> rule)
        {
            return rule
                .Must(value => value is null || value.All(c => c is >= '!' and <= '~'))
                .WithMessage("'{PropertyName}' must contain only printable characters and no spaces.");
        }
    }
}
