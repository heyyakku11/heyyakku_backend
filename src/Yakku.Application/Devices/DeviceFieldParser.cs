using Yakku.Domain.Enums;

namespace Yakku.Application.Devices
{
    internal static class DeviceFieldParser
    {
        public static bool TryParsePlatform(string? value, out DevicePlatform platform)
        {
            platform = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Enum.TryParse(value.Trim(), ignoreCase: true, out platform)
                   && Enum.IsDefined(platform);
        }

        public static bool TryParseNotificationPermission(
            string? value,
            out NotificationPermissionStatus permission)
        {
            permission = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return Enum.TryParse(value.Trim(), ignoreCase: true, out permission)
                   && Enum.IsDefined(permission);
        }
    }
}
