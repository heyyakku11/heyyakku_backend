using Microsoft.Extensions.Options;
using Yakku.Application.Configuration;
using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Storage;

namespace Yakku.Infrastructure.Persistence
{
    public sealed class PollMediaBucketResolver : IPollMediaBucketResolver
    {
        private readonly StorageBucketsOptions _buckets;

        public PollMediaBucketResolver(IOptions<StorageOptions> storageOptions)
        {
            _buckets = storageOptions.Value.Buckets;
        }

        public string Resolve(PollMediaType mediaType)
        {
            return mediaType switch
            {
                PollMediaType.Image => MediaUploadPolicyFactory.ResolveBucket(_buckets.Image, "images"),
                PollMediaType.Video => MediaUploadPolicyFactory.ResolveBucket(_buckets.Video, "videos"),
                PollMediaType.Audio => MediaUploadPolicyFactory.ResolveBucket(_buckets.Audio, "audio"),
                _ => throw new InvalidOperationException("Unsupported poll media type.")
            };
        }
    }
}
