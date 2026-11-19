using Microsoft.Extensions.Configuration;
using Yakku.Domain.Enums;

namespace Yakku.Application.Configuration
{
    /// <summary>Non-secret payment behaviour. Bound from the <c>Payment</c> section / flat <c>PAYMENT_*</c> env keys.</summary>
    public sealed class PaymentOptions
    {
        public const string SectionName = "Payment";

        public const string ProviderVariable = "PAYMENT_PROVIDER";
        public const string CurrencyVariable = "PAYMENT_CURRENCY";
        public const string CaptureModeVariable = "PAYMENT_CAPTURE_MODE";
        public const string OrderExpiryMinutesVariable = "PAYMENT_ORDER_EXPIRY_MINUTES";

        /// <summary>Gateway name. Only <c>Razorpay</c> is supported.</summary>
        public string Provider { get; set; } = "Razorpay";

        /// <summary>ISO 4217 currency for new orders.</summary>
        public string Currency { get; set; } = "INR";

        /// <summary>Automatic or Manual capture. Stored as the enum name.</summary>
        public string CaptureMode { get; set; } = nameof(PaymentCaptureMode.Automatic);

        /// <summary>How long an unpaid internal order remains usable for checkout retries.</summary>
        public int OrderExpiryMinutes { get; set; } = 15;

        public PaymentCaptureMode ParseCaptureMode()
        {
            return Enum.TryParse<PaymentCaptureMode>(CaptureMode, ignoreCase: true, out var mode)
                ? mode
                : PaymentCaptureMode.Automatic;
        }

        public TimeSpan OrderLifetime => TimeSpan.FromMinutes(Math.Clamp(OrderExpiryMinutes, 1, 24 * 60));
    }

    /// <summary>
    /// Razorpay credentials and endpoint. Bound from the <c>Razorpay</c> section / flat <c>RAZORPAY_*</c> env keys.
    /// Secrets never appear in <see cref="ToString"/>, logs or exception messages.
    /// </summary>
    public sealed class RazorpayOptions
    {
        public const string SectionName = "Razorpay";

        public const string EnabledVariable = "RAZORPAY_ENABLED";
        public const string KeyIdVariable = "RAZORPAY_KEY_ID";
        public const string KeySecretVariable = "RAZORPAY_KEY_SECRET";
        public const string WebhookSecretVariable = "RAZORPAY_WEBHOOK_SECRET";
        public const string BaseUrlVariable = "RAZORPAY_BASE_URL";

        public const string DefaultBaseUrl = "https://api.razorpay.com/v1";

        private readonly List<string> _errors = [];

        public bool Enabled { get; private init; }

        /// <summary>Publishable key id. Safe to return to Checkout clients.</summary>
        public string KeyId { get; private init; } = string.Empty;

        /// <summary>Server-only API secret. Never expose to clients.</summary>
        public string KeySecret { get; private init; } = string.Empty;

        /// <summary>Server-only webhook HMAC secret. Never expose to clients.</summary>
        public string WebhookSecret { get; private init; } = string.Empty;

        public string BaseUrl { get; private init; } = DefaultBaseUrl;

        public IReadOnlyList<string> Errors => _errors;

        public static RazorpayOptions FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            return FromReaders(
                () => Clean(section["Enabled"]),
                () => Clean(section["KeyId"]),
                () => Clean(section["KeySecret"]),
                () => Clean(section["WebhookSecret"]),
                () => Clean(section["BaseUrl"]),
                enabledLabel: $"{SectionName}:Enabled",
                keyIdLabel: $"{SectionName}:KeyId",
                keySecretLabel: $"{SectionName}:KeySecret",
                webhookSecretLabel: $"{SectionName}:WebhookSecret",
                baseUrlLabel: $"{SectionName}:BaseUrl",
                defaultEnabled: false);
        }

        private static RazorpayOptions FromReaders(
            Func<string?> readEnabled,
            Func<string?> readKeyId,
            Func<string?> readKeySecret,
            Func<string?> readWebhookSecret,
            Func<string?> readBaseUrl,
            string enabledLabel,
            string keyIdLabel,
            string keySecretLabel,
            string webhookSecretLabel,
            string baseUrlLabel,
            bool defaultEnabled)
        {
            var errors = new List<string>();

            var enabled = defaultEnabled;
            var enabledRaw = readEnabled();
            if (enabledRaw is not null)
            {
                switch (enabledRaw.ToLowerInvariant())
                {
                    case "true" or "1":
                        enabled = true;
                        break;
                    case "false" or "0":
                        enabled = false;
                        break;
                    default:
                        errors.Add($"{enabledLabel} must be true or false.");
                        break;
                }
            }

            var keyId = readKeyId() ?? string.Empty;
            var keySecret = readKeySecret() ?? string.Empty;
            var webhookSecret = readWebhookSecret() ?? string.Empty;
            var baseUrl = readBaseUrl() ?? DefaultBaseUrl;

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                errors.Add($"{baseUrlLabel} must be an absolute http(s) URL.");
            }

            if (enabled)
            {
                var missing = new List<string>();
                if (keyId.Length == 0)
                {
                    missing.Add(keyIdLabel);
                }

                if (keySecret.Length == 0)
                {
                    missing.Add(keySecretLabel);
                }

                if (webhookSecret.Length == 0)
                {
                    missing.Add(webhookSecretLabel);
                }

                if (missing.Count > 0)
                {
                    errors.Add(
                        $"Razorpay is enabled but required configuration is missing: {string.Join(", ", missing)}.");
                }
            }

            var options = new RazorpayOptions
            {
                Enabled = enabled,
                KeyId = keyId,
                KeySecret = keySecret,
                WebhookSecret = webhookSecret,
                BaseUrl = baseUrl.TrimEnd('/')
            };
            options._errors.AddRange(errors);
            return options;
        }

        /// <summary>Stops the application from starting on bad configuration. Messages name keys, never values.</summary>
        public void ThrowIfInvalid()
        {
            if (_errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid Razorpay configuration. " + string.Join(" ", _errors)
                + " Set credentials in the environment (deployment dashboard) or in your local .env file,"
                + $" or set {SectionName}:Enabled=false in appsettings to turn payment gateway calls off.");
        }

        public override string ToString()
        {
            return $"RazorpayOptions {{ Enabled = {Enabled}, KeyIdConfigured = {KeyId.Length > 0}, "
                + $"KeySecretConfigured = {KeySecret.Length > 0}, WebhookSecretConfigured = {WebhookSecret.Length > 0}, "
                + $"BaseUrl = {BaseUrl} }}";
        }

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim().Trim('"').Trim('\'').Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
