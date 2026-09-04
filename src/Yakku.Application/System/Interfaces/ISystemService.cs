using Yakku.Application.System.DTOs;

namespace Yakku.Application.System.Interfaces
{
    public interface ISystemService
    {
        Task<DecryptOtpResponse> DecryptOtpAsync(
            DecryptOtpRequest request,
            CancellationToken cancellationToken = default);
    }
}
