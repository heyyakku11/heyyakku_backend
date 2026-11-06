using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence.Configurations;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        /// <summary>The <see cref="AppException.Field"/> of the error raised when the new card's share code collides.</summary>
        public const string PersonalityShareCodeField = "personalityShareCode";

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

        public async Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
        {
            // Compared on the stored lower-case key, so it matches the case-insensitive unique index.
            var key = anonymousName.ToLowerInvariant();
            return await _context.UserProfiles
                .AnyAsync(profile => EF.Property<string>(profile, UserProfileConfiguration.AnonymousNameKey) == key, cancellationToken);
        }

        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            await _context.Users.AddAsync(user, cancellationToken);

            // The card's share code is registered with the account, in the same save, so codes stay unique across
            // polls, checks and cards.
            await _context.ShareCodes.AddAsync(ShareCode.ForPersonalityCard(user.PersonalityCard), cancellationToken);
        }

        public void ClearTracking() => _context.ChangeTracker.Clear();

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, UserProfileConfiguration.AnonymousNameIndex))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Anonymous name already exists.",
                    "anonymousName");
            }
            catch (DbUpdateException exception) when (
                IsUniqueViolation(exception, PersonalityCardConfiguration.ShareCodeIndex)
                || IsUniqueViolation(exception, "PK_share_codes"))
            {
                // A random code collided: registration retries with a new one.
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Share code already exists.",
                    PersonalityShareCodeField);
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
