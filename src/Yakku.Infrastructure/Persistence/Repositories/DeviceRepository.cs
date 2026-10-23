using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        public const string InstallationIndex = "IX_Devices_InstallationId_Platform";
        public const string PushTokenIndex = "IX_Devices_PushTokenHash";

        private readonly YakkuDbContext _context;

        public DeviceRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Devices.FirstOrDefaultAsync(device => device.Id == id, cancellationToken);
        }

        public Task<Device?> GetByInstallationIdAsync(
            string installationId,
            CancellationToken cancellationToken = default)
        {
            return _context.Devices.FirstOrDefaultAsync(
                device => device.InstallationId == installationId,
                cancellationToken);
        }

        public Task<Device?> GetByInstallationIdAndPlatformAsync(
            string installationId,
            DevicePlatform platform,
            CancellationToken cancellationToken = default)
        {
            return _context.Devices.FirstOrDefaultAsync(
                device => device.InstallationId == installationId && device.Platform == platform,
                cancellationToken);
        }

        public Task<Device?> GetOwnedByInstallationIdAsync(
            Guid? userId,
            Guid? guestId,
            string installationId,
            CancellationToken cancellationToken = default)
        {
            if (userId is null && guestId is null)
            {
                return Task.FromResult<Device?>(null);
            }

            return _context.Devices
                .Where(device => device.InstallationId == installationId
                    && ((userId != null && device.UserId == userId)
                        || (guestId != null && device.GuestId == guestId)))
                .OrderByDescending(device => device.LastSeenAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Device>> GetByPushTokenHashAsync(
            string pushTokenHash,
            CancellationToken cancellationToken = default)
        {
            return await _context.Devices
                .Where(device => device.PushTokenHash == pushTokenHash)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Device>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await _context.Devices
                .Where(device => device.UserId == userId)
                .OrderByDescending(device => device.LastSeenAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Device>> GetPushTargetsByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return await _context.Devices
                .Where(device => device.UserId == userId
                    && device.IsActive
                    && device.PushToken != null
                    && device.NotificationPermission != NotificationPermissionStatus.Denied
                    && _context.UserSessions.Any(session => session.DeviceId == device.Id
                        && session.UserId == userId
                        && session.RevokedAt == null
                        && session.ExpiresAt > utcNow))
                .OrderByDescending(device => device.LastSeenAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> TransferGuestDevicesAsync(
            Guid guestId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var utcNow = DateTime.UtcNow;
            var moved = await _context.Devices
                .Where(device => device.GuestId == guestId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(device => device.UserId, userId)
                        .SetProperty(device => device.GuestId, (Guid?)null)
                        .SetProperty(device => device.UpdatedAt, utcNow),
                    cancellationToken);

            // ExecuteUpdate bypasses the change tracker. A copy still tracked with the old guest owner would be
            // cascade-deleted along with the guest, so drop it; it is re-read from the database when needed.
            foreach (var entry in _context.ChangeTracker.Entries<Device>()
                         .Where(entry => entry.Entity.GuestId == guestId)
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }

            return moved;
        }

        public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
        {
            await _context.Devices.AddAsync(device, cancellationToken);
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action(cancellationToken);
                return;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, InstallationIndex))
            {
                _context.ChangeTracker.Clear();
                throw new AppException(
                    409,
                    ApiErrorCodes.DeviceConflict,
                    "A device with this installation id and platform already exists.",
                    "installationId");
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, PushTokenIndex))
            {
                _context.ChangeTracker.Clear();
                throw new AppException(
                    409,
                    ApiErrorCodes.DeviceConflict,
                    "This push token is already registered to another device.",
                    "pushToken");
            }
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres &&
                   postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
