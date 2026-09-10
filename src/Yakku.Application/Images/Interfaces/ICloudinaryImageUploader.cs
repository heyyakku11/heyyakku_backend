using Yakku.Application.Images.DTOs;

namespace Yakku.Application.Images.Interfaces
{
    public interface ICloudinaryImageUploader
    {
        Task<CloudinaryUploadResult> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);
    }
}
