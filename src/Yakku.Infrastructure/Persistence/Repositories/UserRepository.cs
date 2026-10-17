using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

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
                .Include(user => user.Plan)
                .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);
        }

        public async Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Include(user => user.Profile)
                .FirstOrDefaultAsync(user => user.PhoneNumber == phoneNumber, cancellationToken);
        }

        public async Task<IReadOnlyList<User>> ListAsync(
            UserStatus? status,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users
                .AsNoTracking()
                .Include(user => user.Profile)
                .AsQueryable();

            if (status is UserStatus selected)
            {
                query = query.Where(user => user.Status == selected);
            }

            return await query
                .OrderByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
        {
            return await _context.UserProfiles
                .AnyAsync(profile => profile.AnonymousName == anonymousName, cancellationToken);
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
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_UserProfiles_AnonymousName"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Anonymous name already exists.",
                    "anonymousName");
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "IX_Users_PhoneNumber"))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "An account with this phone number already exists.",
                    "phoneNumber");
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
