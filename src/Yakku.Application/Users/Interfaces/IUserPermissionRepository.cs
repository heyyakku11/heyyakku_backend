using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IUserPermissionRepository
    {
        /// <summary>The user's permissions, or null when the user has no permissions row.</summary>
        Task<UserPermissionsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes only the permissions that are set on <paramref name="request"/> and returns all of them, or null
        /// when the user has no permissions row.
        /// </summary>
        Task<UserPermissionsResponse?> UpdateAsync(
            Guid userId,
            UpdateUserPermissionsRequest request,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
    }
}
