using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IAuthService
    {
        Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);
        Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    }
}
