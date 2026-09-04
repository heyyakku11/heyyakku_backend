using Yakku.Application.System.DTOs;

namespace Yakku.Application.System.Interfaces
{
    public interface ISystemHealthService
    {
        Task<SystemHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);
    }
}
