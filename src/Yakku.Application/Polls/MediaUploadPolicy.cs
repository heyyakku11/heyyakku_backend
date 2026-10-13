using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls
{
    public sealed class MediaUploadPolicy : IMediaUploadPolicy
    {
        public const long DefaultImageMaxBytes = 10 * 1024 * 1024;
        public const long DefaultAudioMaxBytes = 20 * 1024 * 1024;
        public const long DefaultVideoMaxBytes = 100 * 1024 * 1024;
        public const int DefaultVideoMaxDurationSeconds = 60;
        public const int DefaultAudioMaxDurationSeconds = 120;
        public const int DefaultMaxPendingPerUser = 10;
        public const int DefaultMaxConcurrentIncompletePerUser = 5;
        public const long DefaultMaxStorageBytesPerUser = 500L * 1024 * 1024;

        private readonly long _imageMaxBytes;
        private readonly long _audioMaxBytes;
        private readonly long _videoMaxBytes;
        private readonly TimeSpan _videoMaxDuration;
        private readonly TimeSpan _audioMaxDuration;

        public MediaUploadPolicy(
            long imageMaxBytes,
            long audioMaxBytes,
            long videoMaxBytes,
            TimeSpan videoMaxDuration,
            TimeSpan audioMaxDuration,
            int maxPendingPerUser,
            int maxConcurrentIncompletePerUser,
            long maxStorageBytesPerUser,
            TimeSpan uploadUrlLifetime,
            TimeSpan downloadUrlLifetime,
            TimeSpan sessionLifetime)
        {
            _imageMaxBytes = imageMaxBytes;
            _audioMaxBytes = audioMaxBytes;
            _videoMaxBytes = videoMaxBytes;
            _videoMaxDuration = videoMaxDuration;
            _audioMaxDuration = audioMaxDuration;
            MaxPendingPerUser = maxPendingPerUser;
            MaxConcurrentIncompletePerUser = maxConcurrentIncompletePerUser;
            MaxStorageBytesPerUser = maxStorageBytesPerUser;
            UploadUrlLifetime = uploadUrlLifetime;
            DownloadUrlLifetime = downloadUrlLifetime;
            SessionLifetime = sessionLifetime;
        }

        public static MediaUploadPolicy Default { get; } = new(
            DefaultImageMaxBytes,
            DefaultAudioMaxBytes,
            DefaultVideoMaxBytes,
            TimeSpan.FromSeconds(DefaultVideoMaxDurationSeconds),
            TimeSpan.FromSeconds(DefaultAudioMaxDurationSeconds),
            DefaultMaxPendingPerUser,
            DefaultMaxConcurrentIncompletePerUser,
            DefaultMaxStorageBytesPerUser,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24));

        public int MaxPendingPerUser { get; }

        public int MaxConcurrentIncompletePerUser { get; }

        public long MaxStorageBytesPerUser { get; }

        public TimeSpan UploadUrlLifetime { get; }

        public TimeSpan DownloadUrlLifetime { get; }

        public TimeSpan SessionLifetime { get; }

        public long MaxBytes(PollMediaType mediaType)
        {
            return mediaType switch
            {
                PollMediaType.Image => _imageMaxBytes,
                PollMediaType.Audio => _audioMaxBytes,
                PollMediaType.Video => _videoMaxBytes,
                _ => throw new InvalidOperationException("Unsupported poll media type.")
            };
        }

        public TimeSpan? MaxDuration(PollMediaType mediaType)
        {
            return mediaType switch
            {
                PollMediaType.Video => _videoMaxDuration,
                PollMediaType.Audio => _audioMaxDuration,
                PollMediaType.Image => null,
                _ => throw new InvalidOperationException("Unsupported poll media type.")
            };
        }

        public bool TryNormalizeMime(PollMediaType mediaType, string? contentType, out string mimeType)
        {
            return PollMediaCatalog.TryNormalize(mediaType, contentType, out mimeType);
        }

        public string ExtensionFor(string mimeType)
        {
            return PollMediaCatalog.ExtensionFor(mimeType);
        }
    }
}
