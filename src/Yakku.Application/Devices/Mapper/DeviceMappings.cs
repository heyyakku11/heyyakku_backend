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
                InstallationId = device.InstallationId,
                Platform = device.Platform.ToString().ToLowerInvariant(),
                NotificationPermission = device.NotificationPermission?.ToString().ToLowerInvariant(),
                IsActive = device.IsActive,
                LastSeenAt = device.LastSeenAt
            };
        }
    }
}
