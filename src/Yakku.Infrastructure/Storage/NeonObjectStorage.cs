using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Yakku.Application.Storage;
using Yakku.Infrastructure.Configuration;

namespace Yakku.Infrastructure.Storage
{
    public sealed class NeonObjectStorage : IObjectStorage, IDisposable
    {
        private readonly Lazy<AmazonS3Client> _client = new(CreateClient);

        public Task<PresignedUpload> CreateUploadUrlAsync(
            string bucketName,
            string objectKey,
            string contentType,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            var expiresAt = DateTime.UtcNow.Add(lifetime);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = expiresAt,
                ContentType = contentType,
                Protocol = Protocol.HTTPS
            };

            var url = _client.Value.GetPreSignedURL(request);
            IReadOnlyDictionary<string, string> headers = new Dictionary<string, string>
            {
                ["Content-Type"] = contentType
            };

            return Task.FromResult(new PresignedUpload("PUT", url, headers, expiresAt));
        }

        public Task<PresignedDownload> CreateDownloadUrlAsync(
            string bucketName,
            string objectKey,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default)
        {
            var expiresAt = DateTime.UtcNow.Add(lifetime);
            var request = new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Verb = HttpVerb.GET,
                Expires = expiresAt,
                Protocol = Protocol.HTTPS
            };

            var url = _client.Value.GetPreSignedURL(request);
            return Task.FromResult(new PresignedDownload(url, expiresAt));
        }

        public async Task<StoredObjectMetadata?> HeadAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _client.Value.GetObjectMetadataAsync(
                    new GetObjectMetadataRequest
                    {
                        BucketName = bucketName,
                        Key = objectKey
                    },
                    cancellationToken);

                return new StoredObjectMetadata(response.ContentLength, response.Headers.ContentType);
            }
            catch (AmazonS3Exception exception) when (IsMissing(exception))
            {
                return null;
            }
        }

        public async Task<byte[]> ReadPrefixAsync(
            string bucketName,
            string objectKey,
            int length,
            CancellationToken cancellationToken = default)
        {
            using var response = await _client.Value.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey,
                    ByteRange = new ByteRange(0, Math.Max(0, length - 1))
                },
                cancellationToken);

            return await ReadExactlyAsync(response.ResponseStream, length, cancellationToken);
        }

        public async Task<byte[]> ReadObjectAsync(
            string bucketName,
            string objectKey,
            long maxBytes,
            CancellationToken cancellationToken = default)
        {
            if (maxBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxBytes));
            }

            using var response = await _client.Value.GetObjectAsync(
                new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = objectKey
                },
                cancellationToken);

            if (response.ContentLength > maxBytes)
            {
                throw new InvalidOperationException("Stored object exceeds the configured maximum size.");
            }

            var capacity = response.ContentLength > 0
                ? (int)Math.Min(response.ContentLength, maxBytes)
                : (int)Math.Min(maxBytes, int.MaxValue);
            using var memory = new MemoryStream(capacity);
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var count = await response.ResponseStream.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (count == 0)
                {
                    break;
                }

                total += count;
                if (total > maxBytes)
                {
                    throw new InvalidOperationException("Stored object exceeds the configured maximum size.");
                }

                await memory.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
            }

            return memory.ToArray();
        }

        public async Task DeleteAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _client.Value.DeleteObjectAsync(bucketName, objectKey, cancellationToken);
            }
            catch (AmazonS3Exception exception) when (IsMissing(exception))
            {
            }
        }

        public void Dispose()
        {
            if (_client.IsValueCreated)
            {
                _client.Value.Dispose();
            }
        }

        private static AmazonS3Client CreateClient()
        {
            EnvFile.Load();
            var endpoint = EnvFile.GetRequired("AWS_ENDPOINT_URL_S3");
            var region = EnvFile.GetRequired("AWS_REGION");
            var accessKey = EnvFile.GetRequired("AWS_ACCESS_KEY_ID");
            var secret = EnvFile.GetRequired("AWS_SECRET_ACCESS_KEY");
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                AuthenticationRegion = region,
                ForcePathStyle = true,
                UseHttp = endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            };

            return new AmazonS3Client(new BasicAWSCredentials(accessKey, secret), config);
        }

        private static async Task<byte[]> ReadExactlyAsync(
            Stream stream,
            int length,
            CancellationToken cancellationToken)
        {
            using var responseStream = stream;
            var buffer = new byte[length];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await responseStream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken);
                if (count == 0)
                {
                    break;
                }

                read += count;
            }

            return read == buffer.Length ? buffer : buffer[..read];
        }

        private static bool IsMissing(AmazonS3Exception exception)
        {
            return exception.StatusCode == global::System.Net.HttpStatusCode.NotFound
                   || string.Equals(exception.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase);
        }
    }
}
