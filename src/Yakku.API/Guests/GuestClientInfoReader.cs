using System.Net;
using System.Text.RegularExpressions;
using Yakku.Application.Guests.DTOs;

namespace Yakku.API.Guests
{
    /// <summary>
    /// Builds <see cref="GuestClientInfo"/> from request headers only; the guest-create endpoint takes no body.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><c>User-Agent</c> - raw string, also parsed into browser, OS and device type.</item>
    /// <item><c>Accept-Language</c> - the first (preferred) language tag.</item>
    /// <item><c>X-Cookies-Enabled</c> - <c>true</c>/<c>false</c> sent by the client (<c>navigator.cookieEnabled</c>);
    /// the browser cannot send this on its own.</item>
    /// <item><c>CF-IPCountry</c> / <c>X-Vercel-IP-Country</c> - country resolved by the edge proxy. We do no IP
    /// geolocation of our own.</item>
    /// <item>IP address - not a client-chosen header: it is the connection address after the forwarded-headers
    /// middleware has applied the trusted <c>X-Forwarded-For</c> hops.</item>
    /// </list>
    /// </remarks>
    public static partial class GuestClientInfoReader
    {
        public const string CookiesEnabledHeaderName = "X-Cookies-Enabled";

        private static readonly string[] CountryHeaderNames = ["CF-IPCountry", "X-Vercel-IP-Country"];

        // Cloudflare uses XX (unknown) and T1 (Tor) in the same field.
        private static readonly string[] NonCountryCodes = ["XX", "T1"];

        [GeneratedRegex(@"^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8}){0,3}$", RegexOptions.None, matchTimeoutMilliseconds: 50)]
        private static partial Regex LanguageTagPattern();

        public static GuestClientInfo From(HttpContext httpContext)
        {
            var headers = httpContext.Request.Headers;

            var userAgent = headers.UserAgent.ToString();
            var parsed = UserAgentParser.Parse(userAgent);

            return new GuestClientInfo(
                IpAddress: ReadIp(httpContext),
                UserAgent: string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                BrowserName: parsed.BrowserName,
                BrowserVersion: parsed.BrowserVersion,
                OsName: parsed.OsName,
                DeviceType: parsed.DeviceType,
                Language: ReadLanguage(headers.AcceptLanguage.ToString()),
                CountryCode: ReadCountry(headers),
                CookiesEnabled: ReadBool(headers[CookiesEnabledHeaderName].ToString()));
        }

        private static string? ReadIp(HttpContext httpContext)
        {
            var ip = httpContext.Connection.RemoteIpAddress;
            if (ip is null)
            {
                return null;
            }

            if (ip.IsIPv4MappedToIPv6)
            {
                ip = ip.MapToIPv4();
            }

            return ip.ToString();
        }

        private static string? ReadLanguage(string acceptLanguage)
        {
            if (string.IsNullOrWhiteSpace(acceptLanguage))
            {
                return null;
            }

            // "en-IN,en;q=0.9,hi;q=0.8" -> "en-IN"
            var first = acceptLanguage.Split(',', 2)[0].Split(';', 2)[0].Trim();
            return LanguageTagPattern().IsMatch(first) ? first : null;
        }

        private static string? ReadCountry(IHeaderDictionary headers)
        {
            foreach (var name in CountryHeaderNames)
            {
                var value = headers[name].ToString().Trim();
                if (value.Length == 2
                    && value.All(char.IsAsciiLetter)
                    && !NonCountryCodes.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    return value.ToUpperInvariant();
                }
            }

            return null;
        }

        private static bool? ReadBool(string value)
        {
            return value.Trim().ToLowerInvariant() switch
            {
                "true" or "1" => true,
                "false" or "0" => false,
                _ => null
            };
        }
    }
}
