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

        public async Task<Device?> GetByInstallationIdAndPlatformForUpdateAsync(
            string installationId,
            DevicePlatform platform,
            CancellationToken cancellationToken = default)
        {
            // FOR UPDATE makes a concurrent login/register/logout on the same installation wait for this
            // transaction, then re-read the committed row. Platform is stored as its enum name.
            var platformName = platform.ToString();
            var matches = await _context.Devices
                .FromSqlInterpolated(
                    $"SELECT * FROM devices WHERE \"InstallationId\" = {installationId} AND \"Platform\" = {platformName} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return matches.FirstOrDefault();
        }

        public async Task<IReadOnlyList<Device>> GetPushTargetsByUserIdAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            return await PushEligible(utcNow)
                .Where(device => device.UserId == userId)
                .OrderByDescending(device => device.LastSeenAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Device>> GetPushEligibleByIdsAsync(
            IReadOnlyCollection<Guid> deviceIds,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            if (deviceIds.Count == 0)
            {
                return [];
            }

            return await PushEligible(utcNow)
                .AsNoTracking()
                .Where(device => deviceIds.Contains(device.Id))
                .ToListAsync(cancellationToken);
        }

        public async Task<int> DetachBySessionsAsync(
            IReadOnlyCollection<Guid> sessionIds,
            CancellationToken cancellationToken = default)
        {
            if (sessionIds.Count == 0)
            {
                return 0;
            }

            // The WHERE clause is the guard: only installations still pointing at one of these sessions change.
            return await DetachAsync(
                _context.Devices.Where(device => device.CurrentSessionId != null
                    && sessionIds.Contains(device.CurrentSessionId.Value)),
                cancellationToken);
        }

        public async Task<int> DeactivateBySessionAsync(
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var changed = await _context.Devices
                .Where(device => device.CurrentSessionId == sessionId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(device => device.IsActive, false)
                        .SetProperty(device => device.PushToken, (string?)null)
                        .SetProperty(device => device.PushTokenHash, (string?)null)
                        .SetProperty(device => device.UpdatedAt, now),
                    cancellationToken);
            ForgetTrackedDevices(_ => true);
            return changed;
        }

        public Task<int> DetachByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return DetachAsync(_context.Devices.Where(device => device.UserId == userId), cancellationToken);
        }

        public Task<int> DetachWithEndedSessionsAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            return DetachAsync(
                _context.Devices.Where(device => device.CurrentSessionId != null
                    && _context.UserSessions.Any(session => session.Id == device.CurrentSessionId
                        && (session.RevokedAt != null || session.ExpiresAt <= utcNow))),
                cancellationToken);
        }

        public async Task<bool> ClearPushTokenIfMatchesAsync(
            Guid deviceId,
            string pushTokenHash,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var cleared = await _context.Devices
                .Where(device => device.Id == deviceId && device.PushTokenHash == pushTokenHash)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(device => device.PushToken, (string?)null)
                        .SetProperty(device => device.PushTokenHash, (string?)null)
                        .SetProperty(device => device.UpdatedAt, now),
                    cancellationToken);
            ForgetTrackedDevices(device => device.Id == deviceId);
            return cleared > 0;
        }

        /// <summary>
        /// A device may receive push only when: it belongs to a user, is active, has a token, has the notification
        /// permission granted and push switched on (both are per installation, so one user's devices differ); and its
        /// current session is a live session of that same user.
        /// </summary>
        private IQueryable<Device> PushEligible(DateTime utcNow)
        {
            return _context.Devices.Where(device => device.UserId != null
                && device.IsActive
                && device.PushToken != null
                && device.IsPushAllowed
                && device.NotificationPermission == NotificationPermissionStatus.Granted
                && device.CurrentSessionId != null
                && _context.UserSessions.Any(session => session.Id == device.CurrentSessionId
                    && session.UserId == device.UserId
                    && session.RevokedAt == null
                    && session.ExpiresAt > utcNow));
        }

        private async Task<int> DetachAsync(IQueryable<Device> devices, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var detached = await devices.ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(device => device.UserId, (Guid?)null)
                    .SetProperty(device => device.CurrentSessionId, (Guid?)null)
                    .SetProperty(device => device.IsActive, false)
                    .SetProperty(device => device.PushToken, (string?)null)
                    .SetProperty(device => device.PushTokenHash, (string?)null)
                    .SetProperty(device => device.UpdatedAt, now),
                cancellationToken);

            // ExecuteUpdate bypasses the change tracker; drop copies that may now be stale.
            ForgetTrackedDevices(_ => true);
            return detached;
        }

        private void ForgetTrackedDevices(Func<Device, bool> predicate)
        {
            foreach (var entry in _context.ChangeTracker.Entries<Device>()
                         .Where(entry => entry.State == EntityState.Unchanged && predicate(entry.Entity))
                         .ToList())
            {
                entry.State = EntityState.Detached;
            }
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
