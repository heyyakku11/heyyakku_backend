using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.DTOs;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.Images.Mapper;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Images.Services
{
    public class ImageService : IImageService
    {
        private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp",
            "image/gif"
        };

        private const long MaxFileBytes = 10 * 1024 * 1024;

        private readonly ICloudinaryImageUploader _uploader;
        private readonly IImageRepository _images;

        public ImageService(ICloudinaryImageUploader uploader, IImageRepository images)
        {
            _uploader = uploader;
            _images = images;
        }

        public async Task<ImageResponse> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            if (stream is null || stream == Stream.Null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Image file is required.",
                    "file");
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "File name is required.",
                    "file");
            }

            if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Unsupported image type. Allowed: jpeg, png, webp, gif.",
                    "file");
            }

            if (stream.CanSeek && stream.Length > MaxFileBytes)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Image must be 10 MB or smaller.",
                    "file");
            }

            var upload = await _uploader.UploadAsync(stream, fileName, contentType, cancellationToken);
            var image = new Image(
                ImageProvider.Cloudinary,
                upload.PublicId,
                upload.Url,
                upload.SecureUrl,
                upload.ResourceType,
                upload.Format,
                upload.Width,
                upload.Height);

            await _images.AddAsync(image, cancellationToken);
            await _images.SaveChangesAsync(cancellationToken);
            return image.ToResponse();
        }

        public async Task EnsureExistAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
            {
                return;
            }

            var distinct = ids.Distinct().ToList();
            var existing = await _images.GetExistingIdsAsync(distinct, cancellationToken);
            var missing = distinct.Where(id => !existing.Contains(id)).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            throw new AppException(
                400,
                ApiErrorCodes.ValidationError,
                "One or more image IDs are invalid.",
                "options",
                $"Missing image IDs: {string.Join(", ", missing)}");
        }
    }
}
