using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Storage;

namespace Yakku.Infrastructure.Persistence
{
    public sealed class PollMediaBucketResolver : IPollMediaBucketResolver
    {
        public string Resolve(PollMediaType mediaType)
        {
            return mediaType switch
            {
                PollMediaType.Image => MediaUploadPolicyFactory.ResolveBucket("POLL_MEDIA_IMAGE_BUCKET", "images"),
                PollMediaType.Video => MediaUploadPolicyFactory.ResolveBucket("POLL_MEDIA_VIDEO_BUCKET", "videos"),
                PollMediaType.Audio => MediaUploadPolicyFactory.ResolveBucket("POLL_MEDIA_AUDIO_BUCKET", "audio"),
                _ => throw new InvalidOperationException("Unsupported poll media type.")
            };
        }
    }
}
