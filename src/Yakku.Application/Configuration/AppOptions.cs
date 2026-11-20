namespace Yakku.Application.Configuration
{
    public sealed class DatabaseOptions
    {
        public const string SectionName = "Database";

        public string ConnectionString { get; set; } = string.Empty;

        public bool AutoMigrate { get; set; } = true;
    }

    public sealed class RedisOptions
    {
        public const string SectionName = "Redis";

        /// <summary>
        /// Provider-agnostic connection URL from <c>REDIS_URL</c>: <c>redis://</c>, <c>rediss://</c>,
        /// or a StackExchange connection string. Not Upstash-REST specific.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }

    public sealed class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string Secret { get; set; } = string.Empty;
    }

    public sealed class CorsSettings
    {
        public const string SectionName = "Cors";

        public string Origins { get; set; } = string.Empty;
    }

    public sealed class ResendOptions
    {
        public const string SectionName = "Resend";

        public string ApiKey { get; set; } = string.Empty;

        public string OtpTemplateId { get; set; } = string.Empty;

        public ResendAdminTemplatesOptions AdminTemplates { get; set; } = new();
    }

    public sealed class ResendAdminTemplatesOptions
    {
        public string Register { get; set; } = string.Empty;

        public string ForgotPassword { get; set; } = string.Empty;
    }

    public sealed class FirebaseOptions
    {
        public const string SectionName = "Firebase";

        public string Type { get; set; } = "service_account";

        public string ProjectId { get; set; } = string.Empty;

        public string PrivateKeyId { get; set; } = string.Empty;

        public string PrivateKey { get; set; } = string.Empty;

        public string ClientEmail { get; set; } = string.Empty;

        public string ClientId { get; set; } = string.Empty;

        public string AuthUri { get; set; } = "https://accounts.google.com/o/oauth2/auth";

        public string TokenUri { get; set; } = "https://oauth2.googleapis.com/token";

        public string AuthProviderX509CertUrl { get; set; } = "https://www.googleapis.com/oauth2/v1/certs";

        public string ClientX509CertUrl { get; set; } = string.Empty;

        public string UniverseDomain { get; set; } = "googleapis.com";
    }

    public sealed class StorageOptions
    {
        public const string SectionName = "Storage";

        public StorageS3Options S3 { get; set; } = new();

        public StorageBucketsOptions Buckets { get; set; } = new();
    }

    public sealed class StorageS3Options
    {
        public string Endpoint { get; set; } = string.Empty;

        public string Region { get; set; } = string.Empty;

        public string AccessKeyId { get; set; } = string.Empty;

        public string SecretAccessKey { get; set; } = string.Empty;
    }

    public sealed class StorageBucketsOptions
    {
        public string Image { get; set; } = "images";

        public string Video { get; set; } = "videos";

        public string Audio { get; set; } = "audio";
    }

    public sealed class MediaUploadOptions
    {
        public const string SectionName = "MediaUpload";

        public long ImageMaxBytes { get; set; } = Polls.MediaUploadPolicy.DefaultImageMaxBytes;

        public long AudioMaxBytes { get; set; } = Polls.MediaUploadPolicy.DefaultAudioMaxBytes;

        public long VideoMaxBytes { get; set; } = Polls.MediaUploadPolicy.DefaultVideoMaxBytes;

        public int VideoMaxDurationSeconds { get; set; } = Polls.MediaUploadPolicy.DefaultVideoMaxDurationSeconds;

        public int AudioMaxDurationSeconds { get; set; } = Polls.MediaUploadPolicy.DefaultAudioMaxDurationSeconds;

        public int MaxPendingPerUser { get; set; } = Polls.MediaUploadPolicy.DefaultMaxPendingPerUser;

        public int MaxConcurrentIncompletePerUser { get; set; } =
            Polls.MediaUploadPolicy.DefaultMaxConcurrentIncompletePerUser;

        public long MaxStorageBytesPerUser { get; set; } = Polls.MediaUploadPolicy.DefaultMaxStorageBytesPerUser;

        public int UploadUrlTtlSeconds { get; set; } = 900;

        public int DownloadUrlTtlSeconds { get; set; } = 900;

        public int SessionTtlHours { get; set; } = 24;

        public int CleanupIntervalMinutes { get; set; } = 15;
    }

    public sealed class WebOptions
    {
        public const string SectionName = "Web";

        public string BaseUrl { get; set; } = string.Empty;
    }

    public sealed class AdminOptions
    {
        public const string SectionName = "Admin";

        public string WebBaseUrl { get; set; } = string.Empty;

        public string SecretCode { get; set; } = string.Empty;

        public string TrustedOrigins { get; set; } = string.Empty;

        public string CookieName { get; set; } = string.Empty;

        public bool? CookieSecure { get; set; }

        public string CookieSameSite { get; set; } = string.Empty;

        public bool? CsrfEnabled { get; set; }

        public int? LoginRateLimit { get; set; }

        public AdminSessionSettings Session { get; set; } = new();
    }

    public sealed class AdminSessionSettings
    {
        public string IdleTimeout { get; set; } = string.Empty;

        public string AbsoluteTimeout { get; set; } = string.Empty;

        public string RotationInterval { get; set; } = string.Empty;
    }

    public sealed class ForwardedHeadersAppOptions
    {
        public const string SectionName = "ForwardedHeaders";

        public int ProxyHopCount { get; set; } = 1;
    }
}
