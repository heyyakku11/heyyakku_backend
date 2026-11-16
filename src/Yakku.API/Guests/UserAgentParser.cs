using System.Text.RegularExpressions;
using Yakku.Domain.Enums;

namespace Yakku.API.Guests
{
    /// <summary>
    /// Deliberately small User-Agent parser: enough to tell the browser, the OS and the class of device apart for
    /// support and abuse triage. The raw User-Agent is stored too, so a better parser can be applied later.
    /// </summary>
    public static class UserAgentParser
    {
        public readonly record struct Result(
            string? BrowserName,
            string? BrowserVersion,
            string? OsName,
            GuestDeviceType DeviceType);

        private const int MaxInputLength = 512;
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(50);

        // Order matters: the first match wins, and most browsers also claim to be Chrome and Safari.
        private static readonly (string Name, Regex Pattern)[] Browsers =
        [
            ("Edge", Token(@"(?:Edg|EdgA|EdgiOS|Edge)")),
            ("Opera", Token(@"(?:OPR|OPiOS|Opera)")),
            ("Samsung Internet", Token(@"SamsungBrowser")),
            ("Firefox", Token(@"(?:Firefox|FxiOS)")),
            ("Chrome", Token(@"(?:Chrome|CriOS|Chromium)")),
            ("Safari", new Regex(@"Version/(?<v>\d+(?:\.\d+)*).*Safari/", Options, MatchTimeout)),
            ("Dart", Token(@"Dart")),
            ("okhttp", Token(@"okhttp")),
            ("curl", Token(@"curl")),
            ("Postman", Token(@"PostmanRuntime"))
        ];

        private static readonly Regex BotPattern = new(
            @"bot\b|crawler|spider|slurp|headless|facebookexternalhit|bingpreview|lighthouse|python-requests|wget|scrapy",
            Options,
            MatchTimeout);

        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

        public static Result Parse(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return new Result(null, null, null, GuestDeviceType.Unknown);
            }

            var ua = userAgent.Length <= MaxInputLength ? userAgent : userAgent[..MaxInputLength];

            string? browser = null;
            string? version = null;
            foreach (var (name, pattern) in Browsers)
            {
                var match = pattern.Match(ua);
                if (match.Success)
                {
                    browser = name;
                    version = match.Groups["v"].Value;
                    break;
                }
            }

            var os = DetectOs(ua);
            return new Result(browser, version, os, DetectDeviceType(ua, os));
        }

        // iPhone/iPad user agents also say "like Mac OS X" and Android ones say "Linux", so the specific names go first.
        private static string? DetectOs(string ua)
        {
            if (ua.Contains("Android", StringComparison.OrdinalIgnoreCase)) return "Android";
            if (ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase)
                || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                || ua.Contains("iPod", StringComparison.OrdinalIgnoreCase)) return "iOS";
            if (ua.Contains("CrOS", StringComparison.Ordinal)) return "ChromeOS";
            if (ua.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return "Windows";
            if (ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase)
                || ua.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase)) return "macOS";
            if (ua.Contains("Linux", StringComparison.OrdinalIgnoreCase)
                || ua.Contains("X11", StringComparison.OrdinalIgnoreCase)) return "Linux";
            return null;
        }

        private static GuestDeviceType DetectDeviceType(string ua, string? os)
        {
            if (BotPattern.IsMatch(ua))
            {
                return GuestDeviceType.Bot;
            }

            if (ua.Contains("iPad", StringComparison.OrdinalIgnoreCase)
                || ua.Contains("Tablet", StringComparison.OrdinalIgnoreCase)
                || (os == "Android" && !ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase)))
            {
                return GuestDeviceType.Tablet;
            }

            if (os is "Android" or "iOS" || ua.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            {
                return GuestDeviceType.Mobile;
            }

            return os is "Windows" or "macOS" or "Linux" or "ChromeOS"
                ? GuestDeviceType.Desktop
                : GuestDeviceType.Unknown;
        }

        private static Regex Token(string token) =>
            new($@"\b{token}[/ ](?<v>\d+(?:\.\d+)*)", Options, MatchTimeout);
    }
}
