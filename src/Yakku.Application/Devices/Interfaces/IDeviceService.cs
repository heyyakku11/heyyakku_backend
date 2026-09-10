using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Devices.Interfaces
{
    public interface IDeviceService
    {
        Task<RegisterDeviceResult> RegisterAsync(
            Guid userId,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken = default);

        Task<DeviceResponse> UpdateAsync(
            Guid userId,
            string installationId,
            UpdateDeviceRequest request,
            CancellationToken cancellationToken = default);

        Task UnregisterAsync(
            Guid userId,
            string installationId,
            CancellationToken cancellationToken = default);
    }
}
