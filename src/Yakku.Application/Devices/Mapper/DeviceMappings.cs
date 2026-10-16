using Yakku.Application.Devices.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Devices.Mapper
{
    internal static class DeviceMappings
    {
        public static DeviceResponse ToResponse(this Device device, bool isCurrent = false)
        {
            return new DeviceResponse
            {
                InstallationId = device.InstallationId,
                Platform = device.Platform.ToString().ToLowerInvariant(),
                DeviceModel = device.DeviceModel,
                OsVersion = device.OsVersion,
                AppVersion = device.AppVersion,
                AppBuild = device.AppBuild,
                Locale = device.Locale,
                Timezone = device.Timezone,
                NotificationPermission = device.NotificationPermission?.ToString().ToLowerInvariant(),
                IsActive = device.IsActive,
                PushEnabled = device.CanReceivePush,
                IsCurrent = isCurrent,
                LastSeenAt = device.LastSeenAt,
                CreatedAt = device.CreatedAt
            };
        }
    }
}
