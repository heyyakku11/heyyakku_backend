using Yakku.Application.AdminUsers.DTOs;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.AdminUsers.Interfaces
{
    public interface IAdminUserService
    {
        Task<(List<AdminUserResponse> Items, PaginationMeta Meta)> ListUsersAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default);

        Task<(List<AdminGuestResponse> Items, PaginationMeta Meta)> ListGuestsAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
