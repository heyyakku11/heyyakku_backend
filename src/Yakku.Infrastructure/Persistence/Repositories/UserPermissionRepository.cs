using Microsoft.EntityFrameworkCore;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class UserPermissionRepository : IUserPermissionRepository
    {
        private readonly YakkuDbContext _context;

        public UserPermissionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<UserPermissionsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.UserPermissions
                .AsNoTracking()
                .Where(permission => permission.UserId == userId)
                .Select(permission => new UserPermissionsResponse
                {
                    AllowAskBack = permission.AllowAskBack,
                    PushNotification = permission.PushNotification,
                    Sms = permission.Sms,
                    WhatsApp = permission.WhatsApp
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<UserPermissionsResponse?> UpdateAsync(
            Guid userId,
            UpdateUserPermissionsRequest request,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var permission = await _context.UserPermissions
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
            if (permission is null)
            {
                return null;
            }

            // Only the changed columns are written, so two requests that change different permissions do not undo
            // each other.
            permission.Apply(request.AllowAskBack, request.PushNotification, request.Sms, request.WhatsApp, utcNow);
            await _context.SaveChangesAsync(cancellationToken);

            return new UserPermissionsResponse
            {
                AllowAskBack = permission.AllowAskBack,
                PushNotification = permission.PushNotification,
                Sms = permission.Sms,
                WhatsApp = permission.WhatsApp
            };
        }
    }
}
