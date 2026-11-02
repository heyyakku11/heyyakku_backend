using Yakku.Application.AdminUsers.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminUsers.Interfaces
{
    public interface IAdminUserRepository
    {
        /// <summary>Users, newest first, one page at a time. A null status returns every status.</summary>
        Task<AdminPage<AdminUserResponse>> GetUsersAsync(
            UserStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>Guests, newest first, one page at a time. activeOnly: true = not expired, false = expired, null = all.</summary>
        Task<AdminPage<AdminGuestResponse>> GetGuestsAsync(
            bool? activeOnly, DateTime utcNow, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
