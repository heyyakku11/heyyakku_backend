using Yakku.Application.Images.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Images.Mapper
{
    internal static class ImageMappings
    {
        public static ImageResponse ToResponse(this Image image)
        {
            return new ImageResponse
            {
                Id = image.Id,
                Url = image.Url,
                SecureUrl = image.SecureUrl
            };
        }
    }
}
