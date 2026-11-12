using System.Security.Cryptography;
using System.Text;
using Yakku.Application.AdminAuth.Security;

namespace Yakku.API.AdminSecurity
{
    /// <summary>
    /// Admin cookie, CSRF and session settings, read from ADMIN_* environment variables with secure defaults.
    /// <see cref="Load"/> validates everything at startup so a misconfigured production deploy fails to start
    /// instead of running with weakened security.
    /// </summary>
    public sealed class AdminSecuritySettings
    {
        public const string AuthenticationScheme = "AdminCookie";
        public const string CsrfHeaderName = "X-CSRF-Token";

        public string CookieName { get; init; } = string.Empty;
        public bool CookieSecure { get; init; }
        public SameSiteMode CookieSameSite { get; init; }
        public bool CsrfEnabled { get; init; }
        public int LoginRateLimitPerMinute { get; init; }
        public IReadOnlySet<string> TrustedOrigins { get; init; } = new HashSet<string>();
        public AdminSessionOptions Session { get; init; } = new();

        public static AdminSecuritySettings Load(
            bool isProduction,
            string jwtSecret,
            Func<string, string?> env)
        {
            var secure = ParseBool(env("ADMIN_COOKIE_SECURE"), "ADMIN_COOKIE_SECURE") ?? isProduction;
            var csrf = ParseBool(env("ADMIN_CSRF_ENABLED"), "ADMIN_CSRF_ENABLED") ?? true;
            var sameSite = ParseSameSite(env("ADMIN_COOKIE_SAME_SITE"));
            var name = string.IsNullOrWhiteSpace(env("ADMIN_COOKIE_NAME"))
                ? (isProduction ? "__Host-yakku_admin" : "yakku_admin")
                : env("ADMIN_COOKIE_NAME")!.Trim();

            if (isProduction && !secure)
            {
                throw new InvalidOperationException("ADMIN_COOKIE_SECURE must be true in production.");
            }

            if (isProduction && !csrf)
            {
                throw new InvalidOperationException("ADMIN_CSRF_ENABLED cannot be turned off in production.");
            }

            if (sameSite == SameSiteMode.None && !secure)
            {
                throw new InvalidOperationException("ADMIN_COOKIE_SAME_SITE=None requires ADMIN_COOKIE_SECURE=true.");
            }

            if (name.StartsWith("__Host-", StringComparison.Ordinal) && !secure)
            {
                throw new InvalidOperationException("A __Host- cookie name requires ADMIN_COOKIE_SECURE=true.");
            }

            if (name.Any(c => c <= ' ' || c == ';' || c == '=' || c == ','))
            {
                throw new InvalidOperationException("ADMIN_COOKIE_NAME contains characters that are not allowed in a cookie name.");
            }

            var idle = ParseSpan(env("ADMIN_SESSION_IDLE_TIMEOUT"), "ADMIN_SESSION_IDLE_TIMEOUT") ?? TimeSpan.FromMinutes(30);
            var absolute = ParseSpan(env("ADMIN_SESSION_ABSOLUTE_TIMEOUT"), "ADMIN_SESSION_ABSOLUTE_TIMEOUT") ?? TimeSpan.FromHours(12);
            var rotation = ParseSpan(env("ADMIN_SESSION_ROTATION_INTERVAL"), "ADMIN_SESSION_ROTATION_INTERVAL") ?? TimeSpan.FromMinutes(15);
            if (idle < TimeSpan.FromMinutes(1) || absolute < idle || rotation < TimeSpan.FromMinutes(1) || absolute > TimeSpan.FromDays(30))
            {
                throw new InvalidOperationException(
                    "Admin session timeouts are invalid: idle and rotation must be at least 1 minute, absolute must be at least the idle timeout and at most 30 days.");
            }

            var rateLimit = 10;
            var rateLimitRaw = env("ADMIN_LOGIN_RATE_LIMIT");
            if (!string.IsNullOrWhiteSpace(rateLimitRaw)
                && (!int.TryParse(rateLimitRaw, out rateLimit) || rateLimit < 1 || rateLimit > 1000))
            {
                throw new InvalidOperationException("ADMIN_LOGIN_RATE_LIMIT must be a number from 1 to 1000 (requests per minute).");
            }

            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in new[] { "ADMIN_WEB_BASE_URL", "ADMIN_TRUSTED_ORIGINS", "CORS_ORIGINS" })
            {
                foreach (var raw in (env(key) ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var cleaned = raw.Trim('"').TrimEnd('/');
                    if (cleaned.Length == 0)
                    {
                        continue;
                    }

                    if (cleaned == "*" || !Uri.TryCreate(cleaned, UriKind.Absolute, out var uri)
                        || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    {
                        throw new InvalidOperationException(
                            $"{key} contains an invalid origin \"{cleaned}\". Use full origins such as https://admin.example.com.");
                    }

                    origins.Add($"{uri.Scheme}://{uri.Authority}");
                }
            }

            if (isProduction && origins.Count == 0)
            {
                throw new InvalidOperationException(
                    "ADMIN_WEB_BASE_URL (or ADMIN_TRUSTED_ORIGINS) must be set in production so admin requests can be origin-checked.");
            }

            // HKDF keeps CSRF tokens independent of the signing secret itself: leaking one does not expose the other.
            var csrfKey = HKDF.DeriveKey(
                HashAlgorithmName.SHA256,
                Encoding.UTF8.GetBytes(jwtSecret),
                32,
                salt: null,
                info: Encoding.UTF8.GetBytes("yakku-admin-csrf-v1"));

            return new AdminSecuritySettings
            {
                CookieName = name,
                CookieSecure = secure,
                CookieSameSite = sameSite,
                CsrfEnabled = csrf,
                LoginRateLimitPerMinute = rateLimit,
                TrustedOrigins = origins,
                Session = new AdminSessionOptions
                {
                    IdleTimeout = idle,
                    AbsoluteTimeout = absolute,
                    RotationInterval = rotation,
                    CsrfKey = csrfKey
                }
            };
        }

        private static bool? ParseBool(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "true" or "1" => true,
                "false" or "0" => false,
                _ => throw new InvalidOperationException($"{name} must be true or false.")
            };
        }

        private static SameSiteMode ParseSameSite(string? value)
        {
            return (value ?? "Strict").Trim().ToLowerInvariant() switch
            {
                "strict" => SameSiteMode.Strict,
                "lax" => SameSiteMode.Lax,
                "none" => SameSiteMode.None,
                _ => throw new InvalidOperationException("ADMIN_COOKIE_SAME_SITE must be Strict, Lax or None.")
            };
        }

        private static TimeSpan? ParseSpan(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!TimeSpan.TryParse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var span))
            {
                throw new InvalidOperationException($"{name} must be a time span such as 00:30:00.");
            }

            return span;
        }
    }
}
