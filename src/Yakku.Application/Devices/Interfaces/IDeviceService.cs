using Yakku.Application.Devices.DTOs;

namespace Yakku.Application.Devices.Interfaces
{
    public interface IDeviceService
    {
        Task<RegisterDeviceResult> RegisterAsync(
            Guid userId,
            RegisterDeviceRequest request,
            Guid sessionId,
            CancellationToken cancellationToken = default);
    }
}
