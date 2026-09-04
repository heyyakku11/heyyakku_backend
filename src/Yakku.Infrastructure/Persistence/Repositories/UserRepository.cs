using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly YakkuDbContext _context;

        public UserRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(user => user.Profile)
                .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(user => user.Profile)
                .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
        }

        public async Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default)
        {
            return await _context.UserProfiles
                .AnyAsync(profile => profile.DisplayName == displayName, cancellationToken);
        }

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(user, cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_UserProfiles_DisplayName"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Display name already exists.",
                    "displayName");
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Users_Email"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An account with this email already exists.",
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
