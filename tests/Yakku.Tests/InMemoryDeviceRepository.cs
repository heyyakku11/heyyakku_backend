using Yakku.Application.Devices.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

internal sealed class InMemoryDeviceRepository : IDeviceRepository
{
    private readonly List<Device> _devices = [];

    public IReadOnlyList<Device> All => _devices;

    public void Seed(Device device) => _devices.Add(device);

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_devices.FirstOrDefault(device => device.Id == id));

    public Task<Device?> GetByInstallationIdAsync(string installationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_devices.FirstOrDefault(device => device.InstallationId == installationId));

    public Task<Device?> GetByInstallationIdAndPlatformAsync(
        string installationId,
        DevicePlatform platform,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_devices.FirstOrDefault(device =>
            device.InstallationId == installationId && device.Platform == platform));

    public Task<Device?> GetByInstallationIdAndPlatformForUpdateAsync(
        string installationId,
        DevicePlatform platform,
        CancellationToken cancellationToken = default)
        => GetByInstallationIdAndPlatformAsync(installationId, platform, cancellationToken);

    public Task<Device?> GetOwnedByInstallationIdAsync(
        Guid? userId,
        Guid? guestId,
        string installationId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_devices.FirstOrDefault(device =>
            device.InstallationId == installationId
            && ((userId is not null && device.UserId == userId) || (guestId is not null && device.GuestId == guestId))));

    public Task<IReadOnlyList<Device>> GetByPushTokenHashAsync(
        string pushTokenHash,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>(_devices.Where(device => device.PushTokenHash == pushTokenHash).ToList());

    public Task<IReadOnlyList<Device>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>(_devices.Where(device => device.UserId == userId).ToList());

    /// <summary>Ids of devices whose current session is live; the real repository derives this from the sessions.</summary>
    public HashSet<Guid> DevicesWithLiveSession { get; } = [];

    /// <summary>Users whose profile has push switched off.</summary>
    public HashSet<Guid> UsersWithPushDisallowed { get; } = [];

    private IEnumerable<Device> Eligible()
        => _devices.Where(device => device.UserId is Guid userId
            && device.CanReceivePush
            && DevicesWithLiveSession.Contains(device.Id)
            && !UsersWithPushDisallowed.Contains(userId));

    public Task<IReadOnlyList<Device>> GetPushTargetsByUserIdAsync(
        Guid userId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>(Eligible().Where(device => device.UserId == userId).ToList());

    public Task<IReadOnlyList<Device>> GetPushEligibleByIdsAsync(
        IReadOnlyCollection<Guid> deviceIds,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Device>>(Eligible().Where(device => deviceIds.Contains(device.Id)).ToList());

    public Task<int> DetachBySessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken = default)
    {
        var detached = _devices.Where(device => device.CurrentSessionId is Guid id && sessionIds.Contains(id)).ToList();
        detached.ForEach(device => device.Detach());
        return Task.FromResult(detached.Count);
    }

    public Task<int> DeactivateBySessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var matched = _devices.Where(device => device.CurrentSessionId == sessionId).ToList();
        matched.ForEach(device => device.Deactivate());
        return Task.FromResult(matched.Count);
    }

    public Task<int> DetachByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var detached = _devices.Where(device => device.UserId == userId).ToList();
        detached.ForEach(device => device.Detach());
        return Task.FromResult(detached.Count);
    }

    public Task<int> DetachWithEndedSessionsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<bool> ClearPushTokenIfMatchesAsync(Guid deviceId, string pushTokenHash, CancellationToken cancellationToken = default)
    {
        var device = _devices.FirstOrDefault(d => d.Id == deviceId && d.PushTokenHash == pushTokenHash);
        device?.ClearPushToken();
        return Task.FromResult(device is not null);
    }

    public Task<int> TransferGuestDevicesAsync(Guid guestId, Guid userId, CancellationToken cancellationToken = default)
    {
        var moved = _devices.Where(device => device.GuestId == guestId).ToList();
        moved.ForEach(device => device.AssignToUser(userId));
        return Task.FromResult(moved.Count);
    }

    public Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        _devices.Add(device);
        return Task.CompletedTask;
    }

    public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        => action(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
