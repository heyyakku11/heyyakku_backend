using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Users.Interfaces;
using Yakku.Infrastructure.Persistence.Configurations;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AnonymousNameRepository : IAnonymousNameRepository
    {
        private readonly YakkuDbContext _context;

        public AnonymousNameRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<AnonymousNameUpdateResult> TryUpdateAsync(
            Guid userId,
            string anonymousName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var updated = await _context.Users
                    .Where(user => user.Id == userId)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(user => user.AnonymousName, anonymousName)
                            .SetProperty(user => user.UpdatedAt, DateTime.UtcNow),
                        cancellationToken);

                return updated == 0 ? AnonymousNameUpdateResult.ProfileNotFound : AnonymousNameUpdateResult.Updated;
            }
            catch (Exception exception) when (IsAnonymousNameCollision(exception))
            {
                return AnonymousNameUpdateResult.Conflict;
            }
        }

        private static bool IsAnonymousNameCollision(Exception exception)
        {
            var postgres = exception as PostgresException ?? exception.InnerException as PostgresException;
            return postgres is { SqlState: PostgresErrorCodes.UniqueViolation }
                   && string.Equals(postgres.ConstraintName, UserConfiguration.AnonymousNameIndex, StringComparison.Ordinal);
        }
    }
}
