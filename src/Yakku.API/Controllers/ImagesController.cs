using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.DTOs;
using Yakku.Application.Images.Interfaces;

namespace Yakku.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/images")]
    public class ImagesController : ControllerBase
    {
        private readonly IImageService _imageService;

        public ImagesController(IImageService imageService)
        {
            _imageService = imageService;
        }

        [HttpPost]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [ProducesResponseType(typeof(ApiResponse<ImageResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Upload(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(ApiResponse.Fail(
                    "Image file is required.",
                    [
                        new ApiError
                        {
                            Code = ApiErrorCodes.ValidationError,
                            Field = "file",
                            Message = "Image file is required."
                        }
                    ]));
            }

            await using var stream = file.OpenReadStream();
            var result = await _imageService.UploadAsync(
                stream,
                file.FileName,
                file.ContentType,
                cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse.Ok(result, "Image uploaded successfully"));
        }
    }
}
