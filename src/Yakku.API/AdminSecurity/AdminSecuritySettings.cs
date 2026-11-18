using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.Configuration;
using Yakku.Infrastructure.Configuration;

namespace Yakku.API.AdminSecurity
{
    /// <summary>
    /// Admin cookie, CSRF and session settings, read from the Admin configuration section with secure defaults.
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

        /// <summary>Builds nested configuration from flat ADMIN_* / CORS_* keys (used by tests).</summary>
        public static AdminSecuritySettings Load(
            bool isProduction,
            string jwtSecret,
            Func<string, string?> env)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(FlatEnvConfigurationBridge.Build(env))
                .Build();
            return Load(isProduction, jwtSecret, configuration);
        }

        public static AdminSecuritySettings Load(
            bool isProduction,
            string jwtSecret,
            IConfiguration configuration)
        {
            var admin = configuration.GetSection(AdminOptions.SectionName).Get<AdminOptions>() ?? new AdminOptions();
            var corsOrigins = configuration.GetSection(CorsSettings.SectionName)["Origins"];

            var secure = admin.CookieSecure
                ?? ParseBool(configuration["Admin:CookieSecure"], "Admin:CookieSecure")
                ?? isProduction;
            var csrf = admin.CsrfEnabled
                ?? ParseBool(configuration["Admin:CsrfEnabled"], "Admin:CsrfEnabled")
                ?? true;
            var sameSite = ParseSameSite(
                string.IsNullOrWhiteSpace(admin.CookieSameSite)
                    ? configuration["Admin:CookieSameSite"]
                    : admin.CookieSameSite);
            var name = string.IsNullOrWhiteSpace(admin.CookieName)
                ? (isProduction ? "__Host-yakku_admin" : "yakku_admin")
                : admin.CookieName.Trim();

            if (isProduction && !secure)
            {
                throw new InvalidOperationException("Admin:CookieSecure / ADMIN_COOKIE_SECURE must be true in production.");
            }

            if (isProduction && !csrf)
            {
                throw new InvalidOperationException("Admin:CsrfEnabled / ADMIN_CSRF_ENABLED cannot be turned off in production.");
            }

            if (sameSite == SameSiteMode.None && !secure)
            {
                throw new InvalidOperationException("Admin:CookieSameSite=None requires Admin:CookieSecure=true.");
            }

            if (name.StartsWith("__Host-", StringComparison.Ordinal) && !secure)
            {
                throw new InvalidOperationException("A __Host- cookie name requires Admin:CookieSecure=true.");
            }

            if (name.Any(c => c <= ' ' || c == ';' || c == '=' || c == ','))
            {
                throw new InvalidOperationException("Admin:CookieName contains characters that are not allowed in a cookie name.");
            }

            var idle = ParseSpan(admin.Session.IdleTimeout, "Admin:Session:IdleTimeout") ?? TimeSpan.FromMinutes(30);
            var absolute = ParseSpan(admin.Session.AbsoluteTimeout, "Admin:Session:AbsoluteTimeout") ?? TimeSpan.FromHours(12);
            var rotation = ParseSpan(admin.Session.RotationInterval, "Admin:Session:RotationInterval") ?? TimeSpan.FromMinutes(15);
            if (idle < TimeSpan.FromMinutes(1) || absolute < idle || rotation < TimeSpan.FromMinutes(1) || absolute > TimeSpan.FromDays(30))
            {
                throw new InvalidOperationException(
                    "Admin session timeouts are invalid: idle and rotation must be at least 1 minute, absolute must be at least the idle timeout and at most 30 days.");
            }

            var rateLimit = admin.LoginRateLimit ?? 10;
            if (rateLimit < 1 || rateLimit > 1000)
            {
                throw new InvalidOperationException("Admin:LoginRateLimit must be a number from 1 to 1000 (requests per minute).");
            }

            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddOrigins(origins, admin.WebBaseUrl, "Admin:WebBaseUrl");
            AddOrigins(origins, admin.TrustedOrigins, "Admin:TrustedOrigins");
            AddOrigins(origins, corsOrigins, "Cors:Origins");

            if (isProduction && origins.Count == 0)
            {
                throw new InvalidOperationException(
                    "Admin:WebBaseUrl (or Admin:TrustedOrigins) must be set in production so admin requests can be origin-checked.");
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

        private static void AddOrigins(HashSet<string> origins, string? rawList, string key)
        {
            foreach (var raw in (rawList ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
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
            // appsettings placeholders use "" — treat blank the same as unset (default Strict).
            if (string.IsNullOrWhiteSpace(value))
            {
                return SameSiteMode.Strict;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "strict" => SameSiteMode.Strict,
                "lax" => SameSiteMode.Lax,
                "none" => SameSiteMode.None,
                _ => throw new InvalidOperationException("Admin:CookieSameSite must be Strict, Lax or None.")
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
