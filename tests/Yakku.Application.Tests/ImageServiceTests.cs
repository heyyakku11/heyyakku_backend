using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.DTOs;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.Images.Services;
using Yakku.Domain.Entities;
using Xunit;

namespace Yakku.Application.Tests.Images;

public class ImageServiceTests
{
    [Fact]
    public async Task Upload_PersistsImageAndReturnsUrls()
    {
        var images = new FakeImageRepository();
        var uploader = new FakeUploader();
        var service = new ImageService(uploader, images);

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await service.UploadAsync(stream, "photo.png", "image/png");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/v1/yakku/photo.png", result.SecureUrl);
        Assert.Single(images.Items);
    }

    [Fact]
    public async Task Upload_UnsupportedType_Throws()
    {
        var service = new ImageService(new FakeUploader(), new FakeImageRepository());
        await using var stream = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.UploadAsync(stream, "file.txt", "text/plain"));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    [Fact]
    public async Task EnsureExist_MissingIds_Throws()
    {
        var images = new FakeImageRepository();
        images.Existing.Add(Guid.NewGuid());
        var service = new ImageService(new FakeUploader(), images);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.EnsureExistAsync([Guid.NewGuid()]));

        Assert.Equal(400, exception.StatusCode);
    }

    private sealed class FakeUploader : ICloudinaryImageUploader
    {
        public Task<CloudinaryUploadResult> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CloudinaryUploadResult
            {
                PublicId = $"yakku/{fileName}",
                Url = $"http://res.cloudinary.com/demo/image/upload/v1/yakku/{fileName}",
                SecureUrl = $"https://res.cloudinary.com/demo/image/upload/v1/yakku/{fileName}",
                ResourceType = "image",
                Format = "png",
                Width = 100,
                Height = 100
            });
        }
    }

    private sealed class FakeImageRepository : IImageRepository
    {
        public List<Image> Items { get; } = [];
        public HashSet<Guid> Existing { get; } = [];

        public Task AddAsync(Image image, CancellationToken cancellationToken = default)
        {
            Items.Add(image);
            Existing.Add(image.Id);
            return Task.CompletedTask;
        }

        public Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(image => image.Id == id));
        }

        public Task<IReadOnlyCollection<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<Guid> found = ids.Where(Existing.Contains).ToList();
            return Task.FromResult(found);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
