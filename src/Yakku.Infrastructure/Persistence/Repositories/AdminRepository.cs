using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AdminRepository : IAdminRepository
    {
        private readonly YakkuDbContext _context;

        public AdminRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<Admin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Admins.FirstOrDefaultAsync(admin => admin.Id == id, cancellationToken);
        }

        public async Task<Admin?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Admins.FirstOrDefaultAsync(admin => admin.Email == email, cancellationToken);
        }

        public async Task AddAsync(Admin admin, CancellationToken cancellationToken = default)
        {
            await _context.Admins.AddAsync(admin, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Admins_Email"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An admin with this email already exists.",
                    "email");
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
