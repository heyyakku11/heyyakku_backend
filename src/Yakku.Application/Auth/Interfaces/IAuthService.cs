using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IAuthService
    {
        Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default);
        Task<RequestOtpResponse> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default);
        Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// The same OTP check, registration and login as <see cref="VerifyOtpAsync"/>, but no app session or refresh
        /// token is created: <see cref="VerifyOtpResponse.Auth"/> is empty. The website login issues its own token.
        /// </summary>
        Task<VerifyOtpResponse> VerifyOtpForWebAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);
    }
}
