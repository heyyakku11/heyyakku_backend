using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Devices.Interfaces
{
    public interface IDeviceService
    {
        /// <summary>
        /// Registers or upserts the caller's installation. The same installation never produces a second record.
        /// An installation that belongs to someone else is never taken over unless that owner has released it.
        /// </summary>
        Task<RegisterDeviceResult> RegisterAsync(
            DeviceCaller caller,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Partial update of the caller's current installation (push-token rotation, metadata, permission).</summary>
        Task<DeviceResponse> UpdateCurrentAsync(
            DeviceCaller caller,
            string? installationId,
            UpdateDeviceRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops push delivery to the caller's current installation. The record is kept (history, re-registration),
        /// login sessions are not touched, and repeating the call is safe.
        /// </summary>
        Task UnregisterCurrentAsync(
            DeviceCaller caller,
            string? installationId,
            CancellationToken cancellationToken = default);

        /// <summary>The authenticated user's own installations. Push tokens are never returned.</summary>
        Task<IReadOnlyList<DeviceResponse>> ListAsync(
            DeviceCaller caller,
            string? currentInstallationId,
            CancellationToken cancellationToken = default);
    }
}
