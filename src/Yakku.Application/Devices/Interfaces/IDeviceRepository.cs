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
        /// Same as <see cref="GetByInstallationIdAndPlatformAsync"/> but locks the row (<c>FOR UPDATE</c>) until the
        /// ambient transaction ends, so login, register and switch on one installation are serialized. Call it inside
        /// <see cref="ExecuteInTransactionAsync"/>.
        /// </summary>
        Task<Device?> GetByInstallationIdAndPlatformForUpdateAsync(
            string installationId,
            DevicePlatform platform,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The user's devices that may receive push right now: associated with the user, active, with a push token,
        /// whose <em>current</em> session is a live session of that user, and whose account has push enabled.
        /// A device whose session ended (logout, eviction, expiry) or that another account now uses is not returned.
        /// </summary>
        Task<IReadOnlyList<Device>> GetPushTargetsByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Fresh copies of those of the given devices that satisfy the push-target rules <em>right now</em>. Senders
        /// call it immediately before sending so a queued or stale device list can never reach a previous account.
        /// </summary>
        Task<IReadOnlyList<Device>> GetPushEligibleByIdsAsync(
            IReadOnlyCollection<Guid> deviceIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Unassociates every installation whose <em>current</em> session is one of the given sessions. Conditional:
        /// an installation already reassigned to another session is untouched, so a delayed logout is harmless.
        /// Returns the number of installations detached.
        /// </summary>
        Task<int> DetachBySessionsAsync(
            IReadOnlyCollection<Guid> sessionIds,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Stops push to the installation whose current session is the given one (token cleared, inactive) but keeps
        /// the account association. Conditional like <see cref="DetachBySessionsAsync"/>.
        /// </summary>
        Task<int> DeactivateBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default);

        /// <summary>Unassociates every installation currently associated with the user (logout everywhere).</summary>
        Task<int> DetachByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Unassociates installations whose current session is revoked or expired. Returns the count.</summary>
        Task<int> DetachWithEndedSessionsAsync(DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears the push token, but only if it is still the one with the given hash. A token that was refreshed
        /// after the failed send is kept. Returns whether anything was cleared.
        /// </summary>
        Task<bool> ClearPushTokenIfMatchesAsync(
            Guid deviceId,
            string pushTokenHash,
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
