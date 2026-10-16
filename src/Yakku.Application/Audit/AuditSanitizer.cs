using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yakku.Application.Audit
{
    /// <summary>
    /// Central place that keeps credentials, tokens and personal identifiers out of audit and system logs.
    /// </summary>
    public static partial class AuditSanitizer
    {
        public const string Redacted = "[redacted]";

        private const int MaxMetadataEntries = 20;
        private const int MaxMetadataValueLength = 200;

        private static readonly string[] SensitiveKeyFragments =
        [
            "token", "secret", "password", "passwd", "otp", "authorization", "cookie",
            "apikey", "api_key", "api-key", "credential", "refresh", "bearer", "signature"
        ];

        [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]*")]
        private static partial Regex JwtPattern();

        [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._~+/=\-]+")]
        private static partial Regex BearerPattern();

        [GeneratedRegex("(?i)(\"?(?:access[_-]?token|refresh[_-]?token|token|secret|password|otp|authorization|api[_-]?key|cookie)\"?\\s*[:=]\\s*)(\"[^\"]*\"|[^\\s,;&]+)")]
        private static partial Regex KeyValuePattern();

        [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
        private static partial Regex EmailPattern();

        [GeneratedRegex(@"\+?\d[\d\-\s]{6,}\d")]
        private static partial Regex PhonePattern();

        [GeneratedRegex(@"\b\d{4,}\b")]
        private static partial Regex LongDigitsPattern();

        [GeneratedRegex(@"[A-Za-z0-9_\-+/=]{32,}")]
        private static partial Regex LongOpaquePattern();

        [GeneratedRegex(@"[\u0000-\u001F\u007F]+")]
        private static partial Regex ControlCharsPattern();

        [GeneratedRegex(@"(?i)^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")]
        private static partial Regex GuidPattern();

        public static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }

        /// <summary>Strips control characters and redacts tokens, secrets, emails, phone numbers and long digit runs.</summary>
        public static string? SanitizeText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var text = ControlCharsPattern().Replace(value, " ").Trim();
            text = JwtPattern().Replace(text, Redacted);
            text = BearerPattern().Replace(text, "Bearer " + Redacted);
            text = KeyValuePattern().Replace(text, "$1" + Redacted);
            text = EmailPattern().Replace(text, Redacted);
            // Long opaque strings (API keys, hashes) go before the digit rules, which would otherwise split them.
            text = LongOpaquePattern().Replace(text, Redacted);
            text = PhonePattern().Replace(text, Redacted);
            text = LongDigitsPattern().Replace(text, Redacted);
            return Truncate(text, maxLength);
        }

        public static string? SanitizeUserAgent(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return Truncate(ControlCharsPattern().Replace(value, " ").Trim(), 256);
        }

        /// <summary>Masks an email or phone number so rows stay recognisable without exposing the address.</summary>
        public static string? MaskRecipient(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var at = trimmed.IndexOf('@');
            if (at > 0)
            {
                return Truncate($"{trimmed[0]}***@{trimmed[(at + 1)..]}", 128);
            }

            if (trimmed.Count(char.IsDigit) >= 6)
            {
                return $"***{trimmed[^2..]}";
            }

            return Redacted;
        }

        /// <summary>Stable, non-reversible handle for a login identifier so repeated failures can be correlated.</summary>
        public static string? HashSubject(string? subject)
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                return null;
            }

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("subject:" + subject.Trim().ToLowerInvariant()));
            return Convert.ToHexStringLower(bytes);
        }

        /// <summary>Per-poll pseudonym for a voter. Different polls yield unrelated references.</summary>
        public static string PseudonymizeVoter(Guid userId, Guid? pollId)
        {
            var input = $"voter:{pollId:N}:{userId:N}";
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..32];
        }

        /// <summary>Replaces identifiers in a request path so logs group by route and never carry per-user values.</summary>
        public static string? NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            var query = path.IndexOf('?');
            var clean = query >= 0 ? path[..query] : path;
            var segments = clean.Split('/')
                .Select(segment => GuidPattern().IsMatch(segment) || (segment.Length > 0 && segment.All(char.IsDigit))
                    ? "{id}"
                    : segment);
            return Truncate(string.Join('/', segments), 200);
        }

        public static string? SerializeMetadata(IReadOnlyDictionary<string, string?>? metadata)
        {
            if (metadata is null || metadata.Count == 0)
            {
                return null;
            }

            var safe = new SortedDictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in metadata)
            {
                if (safe.Count >= MaxMetadataEntries || string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var key = Truncate(pair.Key.Trim(), 64)!;
                if (IsSensitiveKey(key))
                {
                    continue;
                }

                safe[key] = SanitizeMetadataValue(pair.Value);
            }

            return safe.Count == 0 ? null : JsonSerializer.Serialize(safe);
        }

        private static string? SanitizeMetadataValue(string? value)
        {
            if (value is null)
            {
                return null;
            }

            // Identifiers (guids) are the point of most metadata; keep them readable.
            var trimmed = value.Trim();
            if (GuidPattern().IsMatch(trimmed) || (trimmed.Length <= 12 && long.TryParse(trimmed, out _)))
            {
                return trimmed;
            }

            return SanitizeText(value, MaxMetadataValueLength) ?? string.Empty;
        }

        private static bool IsSensitiveKey(string key)
        {
            var lower = key.ToLowerInvariant();
            return SensitiveKeyFragments.Any(fragment => lower.Contains(fragment, StringComparison.Ordinal));
        }
    }
}
