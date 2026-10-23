using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public YakkuController(IYakkuDirectoryService directoryService)
        {
            _directoryService = directoryService;
        }

        [HttpGet("users")]
        [EndpointSummary("List Users (Admin)")]
        [EndpointDescription("Lists registered users, optionally filtered by status. Admin only.")]
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
        [EndpointSummary("List Guests (Admin)")]
        [EndpointDescription("Lists guest identities, optionally filtered by status. Admin only.")]
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
        [EndpointSummary("List Polls (Admin)")]
        [EndpointDescription("Lists polls with their vote counts, optionally filtered by status. Admin only.")]
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
