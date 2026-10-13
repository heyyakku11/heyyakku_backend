using Yakku.Domain.Enums;

namespace Yakku.Application.Polls
{
    public static class PollMediaCatalog
    {
        private static readonly Dictionary<PollMediaType, string[]> MimeTypes = new()
        {
            [PollMediaType.Image] = ["image/jpeg", "image/png", "image/webp"],
            [PollMediaType.Video] = ["video/mp4", "video/webm"],
            [PollMediaType.Audio] = ["audio/mpeg", "audio/mp4", "audio/wav", "audio/ogg"]
        };

        private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
            ["video/mp4"] = ".mp4",
            ["video/webm"] = ".webm",
            ["audio/mpeg"] = ".mp3",
            ["audio/mp4"] = ".m4a",
            ["audio/wav"] = ".wav",
            ["audio/ogg"] = ".ogg"
        };

        public static bool TryNormalize(PollMediaType mediaType, string? contentType, out string mimeType)
        {
            mimeType = Normalize(contentType);
            return MimeTypes.TryGetValue(mediaType, out var allowed)
                   && allowed.Contains(mimeType, StringComparer.Ordinal);
        }

        public static string ExtensionFor(string mimeType)
        {
            return Extensions[Normalize(mimeType)];
        }

        public static string Normalize(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return string.Empty;
            }

            var separator = contentType.IndexOf(';');
            var value = separator >= 0 ? contentType[..separator] : contentType;
            return value.Trim().ToLowerInvariant();
        }
    }
}
