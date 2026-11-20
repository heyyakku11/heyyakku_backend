using Microsoft.Extensions.Configuration;

namespace Yakku.Infrastructure.Configuration
{
    /// <summary>
    /// Maps existing flat host / <c>.env</c> variable names into nested <see cref="IConfiguration"/> keys
    /// so appsettings sections and typed options work without renaming Render dashboard variables.
    /// Only non-empty values are mapped; empty env entries never wipe appsettings defaults.
    /// </summary>
    public static class FlatEnvConfigurationBridge
    {
        private static readonly (string Flat, string Nested)[] Mappings =
        [
            ("DB_CONNECTION_STRING", "Database:ConnectionString"),
            ("DB_AUTO_MIGRATE", "Database:AutoMigrate"),
            ("REDIS_URL", "Redis:Url"),
            ("MSG91_AUTH_KEY", "Msg91:AuthKey"),
            ("MSG91_FLOW_ID", "Msg91:FlowId"),
            ("MSG91_SENDER_ID", "Msg91:SenderId"),
            ("JWT_SECRET", "Jwt:Secret"),
            ("CORS_ORIGINS", "Cors:Origins"),
            ("RESEND_API_KEY", "Resend:ApiKey"),
            ("FIREBASE_TYPE", "Firebase:Type"),
            ("FIREBASE_PROJECT_ID", "Firebase:ProjectId"),
            ("FIREBASE_PRIVATE_KEY_ID", "Firebase:PrivateKeyId"),
            ("FIREBASE_PRIVATE_KEY", "Firebase:PrivateKey"),
            ("FIREBASE_CLIENT_EMAIL", "Firebase:ClientEmail"),
            ("FIREBASE_CLIENT_ID", "Firebase:ClientId"),
            ("FIREBASE_AUTH_URI", "Firebase:AuthUri"),
            ("FIREBASE_TOKEN_URI", "Firebase:TokenUri"),
            ("FIREBASE_AUTH_PROVIDER_X509_CERT_URL", "Firebase:AuthProviderX509CertUrl"),
            ("FIREBASE_CLIENT_X509_CERT_URL", "Firebase:ClientX509CertUrl"),
            ("FIREBASE_UNIVERSE_DOMAIN", "Firebase:UniverseDomain"),
            ("AWS_ENDPOINT_URL_S3", "Storage:S3:Endpoint"),
            ("AWS_REGION", "Storage:S3:Region"),
            ("AWS_ACCESS_KEY_ID", "Storage:S3:AccessKeyId"),
            ("AWS_SECRET_ACCESS_KEY", "Storage:S3:SecretAccessKey"),
            ("POLL_MEDIA_IMAGE_BUCKET", "Storage:Buckets:Image"),
            ("POLL_MEDIA_VIDEO_BUCKET", "Storage:Buckets:Video"),
            ("POLL_MEDIA_AUDIO_BUCKET", "Storage:Buckets:Audio"),
            ("POLL_MEDIA_IMAGE_MAX_BYTES", "MediaUpload:ImageMaxBytes"),
            ("POLL_MEDIA_AUDIO_MAX_BYTES", "MediaUpload:AudioMaxBytes"),
            ("POLL_MEDIA_VIDEO_MAX_BYTES", "MediaUpload:VideoMaxBytes"),
            ("POLL_MEDIA_VIDEO_MAX_DURATION_SECONDS", "MediaUpload:VideoMaxDurationSeconds"),
            ("POLL_MEDIA_AUDIO_MAX_DURATION_SECONDS", "MediaUpload:AudioMaxDurationSeconds"),
            ("POLL_MEDIA_MAX_PENDING_PER_USER", "MediaUpload:MaxPendingPerUser"),
            ("POLL_MEDIA_MAX_CONCURRENT_INCOMPLETE_PER_USER", "MediaUpload:MaxConcurrentIncompletePerUser"),
            ("POLL_MEDIA_MAX_STORAGE_BYTES_PER_USER", "MediaUpload:MaxStorageBytesPerUser"),
            ("POLL_MEDIA_UPLOAD_URL_TTL_SECONDS", "MediaUpload:UploadUrlTtlSeconds"),
            ("POLL_MEDIA_DOWNLOAD_URL_TTL_SECONDS", "MediaUpload:DownloadUrlTtlSeconds"),
            ("POLL_MEDIA_SESSION_TTL_HOURS", "MediaUpload:SessionTtlHours"),
            ("POLL_MEDIA_CLEANUP_INTERVAL_MINUTES", "MediaUpload:CleanupIntervalMinutes"),
            ("WEB_BASE_URL", "Web:BaseUrl"),
            ("ADMIN_WEB_BASE_URL", "Admin:WebBaseUrl"),
            ("ADMIN_SECRET_CODE", "Admin:SecretCode"),
            ("ADMIN_TRUSTED_ORIGINS", "Admin:TrustedOrigins"),
            ("ADMIN_COOKIE_NAME", "Admin:CookieName"),
            ("ADMIN_COOKIE_SECURE", "Admin:CookieSecure"),
            ("ADMIN_COOKIE_SAME_SITE", "Admin:CookieSameSite"),
            ("ADMIN_CSRF_ENABLED", "Admin:CsrfEnabled"),
            ("ADMIN_LOGIN_RATE_LIMIT", "Admin:LoginRateLimit"),
            ("ADMIN_SESSION_IDLE_TIMEOUT", "Admin:Session:IdleTimeout"),
            ("ADMIN_SESSION_ABSOLUTE_TIMEOUT", "Admin:Session:AbsoluteTimeout"),
            ("ADMIN_SESSION_ROTATION_INTERVAL", "Admin:Session:RotationInterval"),
            ("TRUSTED_PROXY_HOPS", "ForwardedHeaders:ProxyHopCount"),
            ("RAZORPAY_ENABLED", "Razorpay:Enabled"),
            ("RAZORPAY_KEY_ID", "Razorpay:KeyId"),
            ("RAZORPAY_KEY_SECRET", "Razorpay:KeySecret"),
            ("RAZORPAY_WEBHOOK_SECRET", "Razorpay:WebhookSecret"),
            ("RAZORPAY_BASE_URL", "Razorpay:BaseUrl"),
            ("PAYMENT_PROVIDER", "Payment:Provider"),
            ("PAYMENT_CURRENCY", "Payment:Currency"),
            ("PAYMENT_CAPTURE_MODE", "Payment:CaptureMode"),
            ("PAYMENT_ORDER_EXPIRY_MINUTES", "Payment:OrderExpiryMinutes")
        ];

        public static IEnumerable<KeyValuePair<string, string?>> Build(
            Func<string, string?>? read = null)
        {
            read ??= static name => Environment.GetEnvironmentVariable(name);
            var values = new List<KeyValuePair<string, string?>>();

            foreach (var (flat, nested) in Mappings)
            {
                var raw = Clean(read(flat));
                if (raw is null)
                {
                    continue;
                }

                values.Add(new KeyValuePair<string, string?>(nested, raw));
            }

            return values;
        }

        /// <summary>Adds mapped flat env values as the highest-priority configuration source after CreateBuilder.</summary>
        public static IConfigurationBuilder AddFlatEnvironmentBridge(
            this IConfigurationBuilder builder,
            Func<string, string?>? read = null)
        {
            return builder.AddInMemoryCollection(Build(read));
        }

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim().Trim('"').Trim('\'').Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
