using System.Net;
using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Guest
    {
        public Guid Id { get; private set; }
        public string SessionTokenHash { get; private set; } = string.Empty;
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastSeenAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }

        /// <summary>
        /// Client-supplied key that makes guest minting idempotent. Unique when set; null on legacy guests.
        /// Treat as sensitive: the session token is derived from it.
        /// </summary>
        public string? IdempotencyKey { get; private set; }

        // Client details captured once, from request headers, when the guest is created. Every one is optional:
        // a missing or unparseable header leaves the column null and never blocks guest creation.

        /// <summary>Client address at creation; for rate limiting and abuse investigation.</summary>
        public IPAddress? IpAddress { get; private set; }

        /// <summary>Raw User-Agent (trimmed, control characters removed, capped) so it can be re-parsed later.</summary>
        public string? UserAgent { get; private set; }
        public string? BrowserName { get; private set; }
        public string? BrowserVersion { get; private set; }
        public string? OsName { get; private set; }
        public GuestDeviceType? DeviceType { get; private set; }

        /// <summary>Preferred language tag from Accept-Language, for example <c>en-IN</c>.</summary>
        public string? Language { get; private set; }

        /// <summary>ISO 3166-1 alpha-2 code supplied by the edge proxy (never derived by us from the IP).</summary>
        public string? CountryCode { get; private set; }

        /// <summary>What the client reported about cookie support; null when it did not say.</summary>
        public bool? CookiesEnabled { get; private set; }

        /// <summary>
        /// How long a guest identity lives from creation. The one place this is defined: the expiry date stored on the
        /// guest and the lifetime of the <c>yakku_guest</c> cookie both use it.
        /// </summary>
        public static readonly TimeSpan Lifetime = TimeSpan.FromDays(15);

        public const int MaxUserAgentLength = 512;
        public const int MaxBrowserNameLength = 64;
        public const int MaxBrowserVersionLength = 32;
        public const int MaxOsNameLength = 64;
        public const int MaxLanguageLength = 35;
        public const int CountryCodeLength = 2;
        public const int MaxIdempotencyKeyLength = 128;

        private Guest()
        {
        }

        public Guest(string sessionTokenHash, string? idempotencyKey = null)
            : this(sessionTokenHash, DateTime.UtcNow, idempotencyKey)
        {
        }

        public Guest(string sessionTokenHash, DateTime createdAtUtc, string? idempotencyKey = null)
        {
            Id = Guid.NewGuid();
            SessionTokenHash = sessionTokenHash;
            CreatedAt = createdAtUtc;
            LastSeenAt = CreatedAt;
            ExpiresAt = CreatedAt.Add(Lifetime);
            IdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        }

        public bool IsExpired(DateTime utcNow)
        {
            return ExpiresAt <= utcNow;
        }

        public void Touch()
        {
            LastSeenAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Frees the unique idempotency index so a new guest can be minted with the same key after expiry.
        /// Does not revive this guest.
        /// </summary>
        public void ClearIdempotencyKey()
        {
            IdempotencyKey = null;
        }

        private static string? NormalizeIdempotencyKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= MaxIdempotencyKeyLength
                ? trimmed
                : trimmed[..MaxIdempotencyKeyLength];
        }

        /// <summary>Records the client details seen at creation. Values are normalised and capped to column size.</summary>
        public void CaptureClient(
            IPAddress? ipAddress,
            string? userAgent,
            string? browserName,
            string? browserVersion,
            string? osName,
            GuestDeviceType? deviceType,
            string? language,
            string? countryCode,
            bool? cookiesEnabled)
        {
            IpAddress = ipAddress;
            UserAgent = Clean(userAgent, MaxUserAgentLength);
            BrowserName = Clean(browserName, MaxBrowserNameLength);
            BrowserVersion = Clean(browserVersion, MaxBrowserVersionLength);
            OsName = Clean(osName, MaxOsNameLength);
            DeviceType = deviceType;
            Language = Clean(language, MaxLanguageLength);
            CountryCode = NormalizeCountry(countryCode);
            CookiesEnabled = cookiesEnabled;
        }

        private static string? Clean(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var chars = value.Where(c => !char.IsControl(c)).ToArray();
            var text = new string(chars).Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return text.Length <= maxLength ? text : text[..maxLength];
        }

        private static string? NormalizeCountry(string? value)
        {
            var trimmed = value?.Trim();
            if (trimmed is null || trimmed.Length != CountryCodeLength || !trimmed.All(char.IsAsciiLetter))
            {
                return null;
            }

            return trimmed.ToUpperInvariant();
        }
    }
}
