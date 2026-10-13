using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IMediaUploadPolicy
    {
        long MaxBytes(PollMediaType mediaType);

        TimeSpan? MaxDuration(PollMediaType mediaType);

        int MaxPendingPerUser { get; }

        int MaxConcurrentIncompletePerUser { get; }

        long MaxStorageBytesPerUser { get; }

        TimeSpan UploadUrlLifetime { get; }

        TimeSpan DownloadUrlLifetime { get; }

        TimeSpan SessionLifetime { get; }

        bool TryNormalizeMime(PollMediaType mediaType, string? contentType, out string mimeType);

        string ExtensionFor(string mimeType);
    }
}
