using Yakku.Application.AdminUsers.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminUsers.Interfaces
{
    public interface IAdminUserRepository
    {
        /// <summary>Users, newest first, starting after the cursor position. A null status returns every status.</summary>
        Task<AdminPage<AdminUserResponse>> GetUsersAsync(
            UserStatus? status, (DateTime CreatedAt, Guid Id)? after, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>Guests, newest first, starting after the cursor position. activeOnly: true = not expired, false = expired, null = all.</summary>
        Task<AdminPage<AdminGuestResponse>> GetGuestsAsync(
            bool? activeOnly, DateTime utcNow, (DateTime CreatedAt, Guid Id)? after, int pageSize, CancellationToken cancellationToken = default);
    }
}
