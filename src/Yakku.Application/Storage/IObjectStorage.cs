namespace Yakku.Application.Storage
{
    public sealed record PresignedUpload(
        string Method,
        string Url,
        IReadOnlyDictionary<string, string> Headers,
        DateTime ExpiresAt);

    public sealed record PresignedDownload(string Url, DateTime ExpiresAt);

    public sealed record StoredObjectMetadata(long Size, string? ContentType);

    public interface IObjectStorage
    {
        Task<PresignedUpload> CreateUploadUrlAsync(
            string bucketName,
            string objectKey,
            string contentType,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);

        Task<PresignedDownload> CreateDownloadUrlAsync(
            string bucketName,
            string objectKey,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default);

        Task<StoredObjectMetadata?> HeadAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default);

        Task<byte[]> ReadPrefixAsync(
            string bucketName,
            string objectKey,
            int length,
            CancellationToken cancellationToken = default);

        Task<byte[]> ReadObjectAsync(
            string bucketName,
            string objectKey,
            long maxBytes,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default);
    }
}
