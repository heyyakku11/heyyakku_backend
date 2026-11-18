using Microsoft.Extensions.Configuration;
using Yakku.Application.Configuration;

namespace Yakku.Infrastructure.Storage
{
    internal static class MediaUploadPolicyFactory
    {
        public static Application.Polls.MediaUploadPolicy Create(MediaUploadOptions options)
        {
            return new Application.Polls.MediaUploadPolicy(
                PositiveOrDefault(options.ImageMaxBytes, Application.Polls.MediaUploadPolicy.DefaultImageMaxBytes),
                PositiveOrDefault(options.AudioMaxBytes, Application.Polls.MediaUploadPolicy.DefaultAudioMaxBytes),
                PositiveOrDefault(options.VideoMaxBytes, Application.Polls.MediaUploadPolicy.DefaultVideoMaxBytes),
                TimeSpan.FromSeconds(PositiveOrDefault(
                    options.VideoMaxDurationSeconds,
                    Application.Polls.MediaUploadPolicy.DefaultVideoMaxDurationSeconds)),
                TimeSpan.FromSeconds(PositiveOrDefault(
                    options.AudioMaxDurationSeconds,
                    Application.Polls.MediaUploadPolicy.DefaultAudioMaxDurationSeconds)),
                PositiveOrDefault(options.MaxPendingPerUser, Application.Polls.MediaUploadPolicy.DefaultMaxPendingPerUser),
                PositiveOrDefault(
                    options.MaxConcurrentIncompletePerUser,
                    Application.Polls.MediaUploadPolicy.DefaultMaxConcurrentIncompletePerUser),
                PositiveOrDefault(
                    options.MaxStorageBytesPerUser,
                    Application.Polls.MediaUploadPolicy.DefaultMaxStorageBytesPerUser),
                TimeSpan.FromSeconds(PositiveOrDefault(options.UploadUrlTtlSeconds, 900)),
                TimeSpan.FromSeconds(PositiveOrDefault(options.DownloadUrlTtlSeconds, 900)),
                TimeSpan.FromHours(PositiveOrDefault(options.SessionTtlHours, 24)));
        }

        public static void ValidateStorageConfiguration(IConfiguration configuration)
        {
            var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
            Require(storage.S3.Endpoint, "Storage:S3:Endpoint", "AWS_ENDPOINT_URL_S3");
            Require(storage.S3.Region, "Storage:S3:Region", "AWS_REGION");
            Require(storage.S3.AccessKeyId, "Storage:S3:AccessKeyId", "AWS_ACCESS_KEY_ID");
            Require(storage.S3.SecretAccessKey, "Storage:S3:SecretAccessKey", "AWS_SECRET_ACCESS_KEY");
            _ = ResolveBucket(storage.Buckets.Image, "images");
            _ = ResolveBucket(storage.Buckets.Video, "videos");
            _ = ResolveBucket(storage.Buckets.Audio, "audio");
        }

        internal static string ResolveBucket(string? configured, string fallback)
        {
            return string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
        }

        private static long PositiveOrDefault(long value, long fallback) => value > 0 ? value : fallback;

        private static int PositiveOrDefault(int value, int fallback) => value > 0 ? value : fallback;

        private static void Require(string? value, string configKey, string envName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{configKey} is not set. Add {envName} to your .env file or environment variables.");
            }
        }
    }
}
