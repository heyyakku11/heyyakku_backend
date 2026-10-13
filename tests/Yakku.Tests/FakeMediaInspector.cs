using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

internal sealed class FakeMediaInspector : IMediaInspector
{
    public long? DurationMs { get; set; } = 1_000;

    public Exception? ThrowOnInspect { get; set; }

    public MediaInspectionResult Inspect(PollMediaType mediaType, string mimeType, ReadOnlySpan<byte> content)
    {
        if (ThrowOnInspect is not null)
        {
            throw ThrowOnInspect;
        }

        return mediaType == PollMediaType.Image
            ? new MediaInspectionResult(null)
            : new MediaInspectionResult(DurationMs);
    }
}
