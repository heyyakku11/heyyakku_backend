using Yakku.Application.AppVersion.DTOs;

namespace Yakku.Application.AppVersion.Interfaces
{
    public interface IAppVersionService
    {
        Task<AppVersionCheckResponse> CheckAsync(
            AppVersionCheckRequest request,
            CancellationToken cancellationToken = default);
    }
}
