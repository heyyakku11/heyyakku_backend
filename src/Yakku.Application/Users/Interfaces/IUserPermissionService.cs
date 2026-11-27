using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IUserPermissionService
    {
        Task<UserPermissionsResponse> GetAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserPermissionsResponse> UpdateAsync(
            Guid userId,
            UpdateUserPermissionsRequest request,
            CancellationToken cancellationToken = default);
    }
}
