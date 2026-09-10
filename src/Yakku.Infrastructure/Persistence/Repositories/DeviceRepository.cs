using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class DeviceRepository : IDeviceRepository
    {
        private readonly YakkuDbContext _context;

        public DeviceRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<Device?> GetByInstallationIdAsync(
            string installationId,
            CancellationToken cancellationToken = default)
        {
            return _context.Devices.FirstOrDefaultAsync(
                device => device.InstallationId == installationId,
                cancellationToken);
        }

        public Task<Device?> GetByInstallationIdAndUserIdAsync(
            string installationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return _context.Devices.FirstOrDefaultAsync(
                device => device.InstallationId == installationId && device.UserId == userId,
                cancellationToken);
        }

        public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
        {
            await _context.Devices.AddAsync(device, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Devices_InstallationId"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "A device with this installation id already exists.",
                    "installationId");
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
