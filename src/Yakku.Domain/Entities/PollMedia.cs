using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class PollMedia
    {
        public Guid Id { get; private set; }
        public Guid PollId { get; private set; }
        public PollMediaType MediaType { get; private set; }
        public string BucketName { get; private set; } = string.Empty;
        public string ObjectKey { get; private set; } = string.Empty;
        public string MimeType { get; private set; } = string.Empty;
        public long FileSize { get; private set; }
        public long? DurationMs { get; private set; }
        public string? ThumbnailObjectKey { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Poll Poll { get; private set; } = null!;

        private PollMedia()
        {
        }

        public PollMedia(
            Guid pollId,
            PollMediaType mediaType,
            string bucketName,
            string objectKey,
            string mimeType,
            long fileSize,
            long? durationMs,
            string? thumbnailObjectKey)
        {
            Id = Guid.NewGuid();
            PollId = pollId;
            MediaType = mediaType;
            BucketName = bucketName;
            ObjectKey = objectKey;
            MimeType = mimeType;
            FileSize = fileSize;
            DurationMs = durationMs;
            ThumbnailObjectKey = thumbnailObjectKey;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
