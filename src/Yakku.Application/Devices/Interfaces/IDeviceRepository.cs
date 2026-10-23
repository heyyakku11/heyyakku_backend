using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Devices.Interfaces
{
    public interface IDeviceRepository
    {
        Task<Device?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default);

        Task<Device?> GetByInstallationIdAsync(
            string installationId,
            CancellationToken cancellationToken = default);

        Task<Device?> GetByInstallationIdAndPlatformAsync(
            string installationId,
            DevicePlatform platform,
            CancellationToken cancellationToken = default);

        /// <summary>The installation, but only if it belongs to the given user or guest.</summary>
        Task<Device?> GetOwnedByInstallationIdAsync(
            Guid? userId,
            Guid? guestId,
            string installationId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Device>> GetByPushTokenHashAsync(
            string pushTokenHash,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<Device>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The user's devices that may receive push right now: active, with a push token, and linked to at least one
        /// login session that is neither revoked nor expired at <paramref name="utcNow"/>. A device whose only
        /// sessions have ended (logout, eviction, expiry) is not returned, whatever its own flags say.
        /// </summary>
        Task<IReadOnlyList<Device>> GetPushTargetsByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Moves every installation owned by the guest to the user in one statement. Call it inside the guest
        /// claim transaction. Returns the number of installations transferred.
        /// </summary>
        Task<int> TransferGuestDevicesAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default);

        Task AddAsync(Device device, CancellationToken cancellationToken = default);

        /// <summary>Runs the action in one transaction (joining an ambient one). Any exception rolls back and is rethrown.</summary>
        Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
