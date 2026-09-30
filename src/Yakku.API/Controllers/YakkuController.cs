using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Categories.DTOs;
using Yakku.Application.Categories.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.YakkuDirectory.DTOs;
using Yakku.Application.YakkuDirectory.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku")]
    public class YakkuController : ControllerBase
    {
        private readonly IYakkuDirectoryService _directoryService;
        private readonly ICategoryService _categoryService;

        public YakkuController(
            IYakkuDirectoryService directoryService,
            ICategoryService categoryService)
        {
            _directoryService = directoryService;
            _categoryService = categoryService;
        }

        [HttpGet("users")]
        [ProducesResponseType(typeof(ApiResponse<List<YakkuUserProfileResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? status,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _directoryService.ListUsersAsync(status, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Users retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("guests")]
        [ProducesResponseType(typeof(ApiResponse<List<YakkuGuestProfileResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetGuests(
            [FromQuery] string? status,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _directoryService.ListGuestsAsync(status, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Guests retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpGet("polls")]
        [ProducesResponseType(typeof(ApiResponse<List<YakkuPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPolls(
            [FromQuery] string? status,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _directoryService.ListPollsAsync(status, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Polls retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("categories")]
        [ProducesResponseType(typeof(ApiResponse<CategoryResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateCategory(
            [FromBody] CreateCategoryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _categoryService.CreateAsync(request, cancellationToken);
                return StatusCode(
                    StatusCodes.Status201Created,
                    ApiResponse.Ok(result, "Category created successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        private static ObjectResult ToErrorResult(AppException exception)
        {
            var response = ApiResponse.Fail(
                exception.Message,
                [
                    new ApiError
                    {
                        Code = exception.ErrorCode,
                        Message = exception.ErrorMessage,
                        Field = exception.Field
                    }
                ]);

            return new ObjectResult(response)
            {
                StatusCode = exception.StatusCode
            };
        }
    }
}
