using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminSessionService
    {
        Task<TokenResponse> CreateAsync(
            Guid adminId,
            string email,
            CancellationToken cancellationToken = default);

        Task<TokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default);
    }
}
