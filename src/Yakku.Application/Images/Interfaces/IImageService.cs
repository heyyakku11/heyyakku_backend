using Yakku.Application.Images.DTOs;

namespace Yakku.Application.Images.Interfaces
{
    public interface IImageService
    {
        Task<ImageResponse> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);

        Task EnsureExistAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default);
    }
}
