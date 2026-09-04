using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Interfaces
{
    public interface ISessionService
    {
        Task<TokenResponse> CreateAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
    }
}
