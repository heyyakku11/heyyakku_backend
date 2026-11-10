using Microsoft.EntityFrameworkCore;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserSettingsRepository : IUserSettingsRepository
    {
        private readonly YakkuDbContext _context;

        public UserSettingsRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<UserSettingsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.UserProfiles
                .AsNoTracking()
                .Where(profile => profile.UserId == userId)
                .Select(profile => new UserSettingsResponse { AllowAskBack = profile.AllowAskBack })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<bool> SetAllowAskBackAsync(
            Guid userId,
            bool allow,
            CancellationToken cancellationToken = default)
        {
            var updated = await _context.UserProfiles
                .Where(profile => profile.UserId == userId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(profile => profile.AllowAskBack, allow)
                        .SetProperty(profile => profile.UpdatedAt, DateTime.UtcNow),
                    cancellationToken);
            return updated > 0;
        }
    }
}
