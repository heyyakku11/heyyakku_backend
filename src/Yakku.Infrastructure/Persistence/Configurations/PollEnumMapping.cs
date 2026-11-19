using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal static class PollEnumMapping
    {
        public static PropertyBuilder<TEnum> HasUppercaseEnumConversion<TEnum>(
            this PropertyBuilder<TEnum> property)
            where TEnum : struct, Enum
        {
            return property.HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<TEnum>(value, ignoreCase: true));
        }

        public static PropertyBuilder<TEnum?> HasUppercaseEnumConversion<TEnum>(
            this PropertyBuilder<TEnum?> property)
            where TEnum : struct, Enum
        {
            var converter = new ValueConverter<TEnum?, string?>(
                value => value.HasValue ? value.Value.ToString().ToUpperInvariant() : null,
                value => string.IsNullOrWhiteSpace(value)
                    ? null
                    : Enum.Parse<TEnum>(value, ignoreCase: true));
            return property.HasConversion(converter);
        }
    }
}
