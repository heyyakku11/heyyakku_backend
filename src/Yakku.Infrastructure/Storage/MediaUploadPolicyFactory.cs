using Yakku.Infrastructure.Configuration;

namespace Yakku.Infrastructure.Storage
{
    internal static class MediaUploadPolicyFactory
    {
        public static Application.Polls.MediaUploadPolicy Create()
        {
            EnvFile.Load();
            return new Application.Polls.MediaUploadPolicy(
                ReadLong("POLL_MEDIA_IMAGE_MAX_BYTES", Application.Polls.MediaUploadPolicy.DefaultImageMaxBytes),
                ReadLong("POLL_MEDIA_AUDIO_MAX_BYTES", Application.Polls.MediaUploadPolicy.DefaultAudioMaxBytes),
                ReadLong("POLL_MEDIA_VIDEO_MAX_BYTES", Application.Polls.MediaUploadPolicy.DefaultVideoMaxBytes),
                ReadSeconds(
                    "POLL_MEDIA_VIDEO_MAX_DURATION_SECONDS",
                    Application.Polls.MediaUploadPolicy.DefaultVideoMaxDurationSeconds),
                ReadSeconds(
                    "POLL_MEDIA_AUDIO_MAX_DURATION_SECONDS",
                    Application.Polls.MediaUploadPolicy.DefaultAudioMaxDurationSeconds),
                ReadInt("POLL_MEDIA_MAX_PENDING_PER_USER", Application.Polls.MediaUploadPolicy.DefaultMaxPendingPerUser),
                ReadInt(
                    "POLL_MEDIA_MAX_CONCURRENT_INCOMPLETE_PER_USER",
                    Application.Polls.MediaUploadPolicy.DefaultMaxConcurrentIncompletePerUser),
                ReadLong(
                    "POLL_MEDIA_MAX_STORAGE_BYTES_PER_USER",
                    Application.Polls.MediaUploadPolicy.DefaultMaxStorageBytesPerUser),
                ReadSeconds("POLL_MEDIA_UPLOAD_URL_TTL_SECONDS", 900),
                ReadSeconds("POLL_MEDIA_DOWNLOAD_URL_TTL_SECONDS", 900),
                ReadHours("POLL_MEDIA_SESSION_TTL_HOURS", 24));
        }

        public static void ValidateStorageConfiguration()
        {
            EnvFile.Load();
            _ = EnvFile.GetRequired("AWS_ENDPOINT_URL_S3");
            _ = EnvFile.GetRequired("AWS_REGION");
            _ = EnvFile.GetRequired("AWS_ACCESS_KEY_ID");
            _ = EnvFile.GetRequired("AWS_SECRET_ACCESS_KEY");
            _ = ResolveBucket("POLL_MEDIA_IMAGE_BUCKET", "images");
            _ = ResolveBucket("POLL_MEDIA_VIDEO_BUCKET", "videos");
            _ = ResolveBucket("POLL_MEDIA_AUDIO_BUCKET", "audio");
        }

        internal static string ResolveBucket(string key, string fallback)
        {
            EnvFile.Load();
            var raw = Environment.GetEnvironmentVariable(key)?.Trim().Trim('"').Trim('\'');
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw;
        }

        private static long ReadLong(string key, long fallback)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            return long.TryParse(raw, out var value) && value > 0 ? value : fallback;
        }

        private static int ReadInt(string key, int fallback)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
        }

        private static TimeSpan ReadSeconds(string key, int fallbackSeconds)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            return int.TryParse(raw, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(fallbackSeconds);
        }

        private static TimeSpan ReadHours(string key, int fallbackHours)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            return int.TryParse(raw, out var hours) && hours > 0
                ? TimeSpan.FromHours(hours)
                : TimeSpan.FromHours(fallbackHours);
        }
    }
}
