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
