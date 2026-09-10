using Yakku.Domain.Entities;

namespace Yakku.Application.Devices.Interfaces
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByInstallationIdAsync(
            string installationId,
            CancellationToken cancellationToken = default);

        Task<Device?> GetByInstallationIdAndUserIdAsync(
            string installationId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(Device device, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
