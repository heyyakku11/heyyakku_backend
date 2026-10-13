using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    }
}
