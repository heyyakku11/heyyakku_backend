using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.DTOs;
using Yakku.Application.Images.Interfaces;
using Yakku.Infrastructure.Configuration;

namespace Yakku.Infrastructure.Cloudinary
{
    public class CloudinaryImageUploader : ICloudinaryImageUploader
    {
        private readonly CloudinaryDotNet.Cloudinary _cloudinary;
        private readonly ILogger<CloudinaryImageUploader> _logger;

        public CloudinaryImageUploader(ILogger<CloudinaryImageUploader> logger)
        {
            _logger = logger;
            var account = new Account(
                EnvFile.GetRequired("CLOUDINARY_CLOUD_NAME"),
                EnvFile.GetRequired("CLOUDINARY_API_KEY"),
                EnvFile.GetRequired("CLOUDINARY_API_SECRET"));
            _cloudinary = new CloudinaryDotNet.Cloudinary(account);
            _cloudinary.Api.Secure = true;
        }

        public async Task<CloudinaryUploadResult> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(fileName, stream),
                    Folder = "yakku",
                    UseFilename = true,
                    UniqueFilename = true,
                    Overwrite = false
                };

                var result = await _cloudinary.UploadAsync(uploadParams, cancellationToken);
                if (result.Error is not null || string.IsNullOrWhiteSpace(result.PublicId))
                {
                    _logger.LogError(
                        "Cloudinary upload failed: {Message}",
                        result.Error?.Message ?? "Unknown error");
                    throw new AppException(
                        502,
                        ApiErrorCodes.InternalServerError,
                        "Image upload failed. Please try again.");
                }

                return new CloudinaryUploadResult
                {
                    PublicId = result.PublicId,
                    Url = result.Url?.ToString() ?? string.Empty,
                    SecureUrl = result.SecureUrl?.ToString() ?? string.Empty,
                    ResourceType = result.ResourceType,
                    Format = result.Format,
                    Width = result.Width,
                    Height = result.Height
                };
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Cloudinary upload threw unexpectedly.");
                throw new AppException(
                    502,
                    ApiErrorCodes.InternalServerError,
                    "Image upload failed. Please try again.");
            }
        }
    }
}
