using Yakku.Application.Polls.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Storage
{
    public sealed class TagLibMediaInspector : IMediaInspector
    {
        public MediaInspectionResult Inspect(
            PollMediaType mediaType,
            string mimeType,
            ReadOnlySpan<byte> content)
        {
            if (mediaType == PollMediaType.Image)
            {
                return new MediaInspectionResult(DurationMs: null);
            }

            if (content.IsEmpty)
            {
                throw new InvalidOperationException("Media content is empty.");
            }

            var bytes = content.ToArray();
            using var stream = new MemoryStream(bytes, writable: false);
            using var file = TagLib.File.Create(new MemoryFileAbstraction("media" + ExtensionFor(mimeType), stream));
            var duration = file.Properties.Duration;
            if (duration <= TimeSpan.Zero)
            {
                throw new InvalidOperationException("Media duration could not be determined.");
            }

            return new MediaInspectionResult((long)Math.Round(duration.TotalMilliseconds));
        }

        private static string ExtensionFor(string mimeType)
        {
            return mimeType.ToLowerInvariant() switch
            {
                "video/mp4" => ".mp4",
                "video/webm" => ".webm",
                "audio/mpeg" => ".mp3",
                "audio/mp4" => ".m4a",
                "audio/wav" => ".wav",
                "audio/ogg" => ".ogg",
                _ => ".bin"
            };
        }

        private sealed class MemoryFileAbstraction : TagLib.File.IFileAbstraction
        {
            public MemoryFileAbstraction(string name, Stream stream)
            {
                Name = name;
                ReadStream = stream;
                WriteStream = stream;
            }

            public string Name { get; }

            public Stream ReadStream { get; }

            public Stream WriteStream { get; }

            public void CloseStream(Stream stream)
            {
            }
        }
    }
}
