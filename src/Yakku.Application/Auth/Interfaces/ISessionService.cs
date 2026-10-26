using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Interfaces
{
    public interface ISessionService
    {
        Task<TokenResponse> CreateAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a session and, when an installation is given, atomically binds it to that session (the OTP login
        /// path). A failing device binding never fails the login: the session is then created unbound.
        /// </summary>
        Task<TokenResponse> CreateAsync(
            Guid userId,
            Yakku.Application.Devices.DTOs.RegisterDeviceRequest? installation,
            CancellationToken cancellationToken = default);
        Task<TokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
        Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
