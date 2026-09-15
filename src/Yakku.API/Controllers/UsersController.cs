using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IPollService _pollService;

        public UsersController(IUserService userService, IPollService pollService)
        {
            _userService = userService;
            _pollService = pollService;
        }

        [HttpGet("me")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
        {
            var user = await _userService.GetMeAsync(User.GetRequiredUserId(), cancellationToken);
            return user is null
                ? NotFound(ApiResponse.NotFound<UserProfileResponse>("User not found"))
                : Ok(ApiResponse.Ok(user, "User retrieved successfully"));
        }

        [HttpGet("me/asked-polls")]
        [ProducesResponseType(typeof(ApiResponse<List<UserPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAskedPolls(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetAskedPollsAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [HttpGet("me/answered-polls")]
        [ProducesResponseType(typeof(ApiResponse<List<UserPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAnsweredPolls(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetAnsweredPollsAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [HttpPost("me/polls/{id:guid}/close")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> ClosePoll(Guid id, CancellationToken cancellationToken)
        {
            var poll = await _pollService.ClosePollAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(poll, "Poll closed successfully"));
        }

        [HttpDelete("me/polls/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePoll(Guid id, CancellationToken cancellationToken)
        {
            await _pollService.DeletePollAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok<object?>(null, "Poll deleted successfully"));
        }
    }
}
