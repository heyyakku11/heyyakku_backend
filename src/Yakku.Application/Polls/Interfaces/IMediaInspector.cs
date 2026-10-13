using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Interfaces
{
    public sealed record MediaInspectionResult(long? DurationMs);

    public interface IMediaInspector
    {
        MediaInspectionResult Inspect(
            PollMediaType mediaType,
            string mimeType,
            ReadOnlySpan<byte> content);
    }
}
