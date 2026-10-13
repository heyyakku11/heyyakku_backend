using Yakku.Domain.Enums;

namespace Yakku.Application.Polls
{
    public static class MediaSignature
    {
        public static bool Matches(PollMediaType mediaType, string mimeType, ReadOnlySpan<byte> prefix)
        {
            if (!PollMediaCatalog.TryNormalize(mediaType, mimeType, out var mime) || prefix.Length < 4)
            {
                return false;
            }

            return mime switch
            {
                "image/jpeg" => StartsWith(prefix, 0xFF, 0xD8, 0xFF),
                "image/png" => StartsWith(prefix, 0x89, 0x50, 0x4E, 0x47),
                "image/webp" => IsRiff(prefix, "WEBP"),
                "audio/wav" => IsRiff(prefix, "WAVE"),
                "audio/ogg" => StartsWithAscii(prefix, "OggS"),
                "audio/mpeg" => IsMp3(prefix),
                "video/webm" => StartsWith(prefix, 0x1A, 0x45, 0xDF, 0xA3),
                "video/mp4" or "audio/mp4" => IsIsoBmff(prefix),
                _ => false
            };
        }

        private static bool IsMp3(ReadOnlySpan<byte> prefix)
        {
            if (StartsWithAscii(prefix, "ID3"))
            {
                return true;
            }

            return prefix.Length >= 2 && prefix[0] == 0xFF && (prefix[1] & 0xE0) == 0xE0;
        }

        private static bool IsIsoBmff(ReadOnlySpan<byte> prefix)
        {
            return prefix.Length >= 8 && StartsWithAscii(prefix[4..], "ftyp");
        }

        private static bool IsRiff(ReadOnlySpan<byte> prefix, string form)
        {
            return prefix.Length >= 12
                   && StartsWithAscii(prefix, "RIFF")
                   && StartsWithAscii(prefix[8..], form);
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> prefix, string value)
        {
            if (prefix.Length < value.Length)
            {
                return false;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (prefix[i] != value[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool StartsWith(ReadOnlySpan<byte> prefix, params byte[] expected)
        {
            if (prefix.Length < expected.Length)
            {
                return false;
            }

            for (var i = 0; i < expected.Length; i++)
            {
                if (prefix[i] != expected[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
