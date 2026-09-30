using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminAuthService
    {
        Task<RequestOtpResponse> RegisterAsync(
            AdminRegisterRequest request,
            CancellationToken cancellationToken = default);

        Task<TokenResponse> VerifyOtpAsync(
            AdminVerifyOtpRequest request,
            CancellationToken cancellationToken = default);

        Task<TokenResponse> LoginAsync(
            AdminLoginRequest request,
            CancellationToken cancellationToken = default);
    }
}
