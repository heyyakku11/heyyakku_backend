using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;

namespace Yakku.Application.Tests.Fakes;

public sealed class FakeSystemLogWriter : ISystemLogWriter
{
    public List<SystemLogWriteRequest> Entries { get; } = [];

    public Task WriteAsync(SystemLogWriteRequest request, CancellationToken cancellationToken = default)
    {
        Entries.Add(request);
        return Task.CompletedTask;
    }
}
