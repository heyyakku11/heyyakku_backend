using Yakku.Application.System.DTOs;

namespace Yakku.Application.System.Interfaces
{
    public interface ISystemLogWriter
    {
        Task WriteAsync(SystemLogWriteRequest request, CancellationToken cancellationToken = default);
    }
}
