using Yakku.Application.Devices.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Devices.Mapper
{
    internal static class DeviceMappings
    {
        public static DeviceResponse ToResponse(this Device device)
        {
            return new DeviceResponse
            {
                Id = device.Id,
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
                LastSeenAt = device.LastSeenAt
            };
        }
    }
}
