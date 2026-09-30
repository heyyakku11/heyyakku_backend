using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;
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
        private readonly ISavedPollService _savedPollService;
        private readonly INotificationService _notificationService;

        public UsersController(
            IUserService userService,
            IPollService pollService,
            ISavedPollService savedPollService,
            INotificationService notificationService)
        {
            _userService = userService;
            _pollService = pollService;
            _savedPollService = savedPollService;
            _notificationService = notificationService;
        }

        [HttpGet()]
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

        [HttpGet("asked-polls")]
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

        [HttpGet("answered-polls")]
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

        [HttpGet("saved-polls")]
        [ProducesResponseType(typeof(ApiResponse<List<PollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSavedPolls(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _savedPollService.ListAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Saved polls retrieved successfully", result.Meta));
        }

        [HttpPost("save-poll/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SavedPollStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SavePoll(Guid id, CancellationToken cancellationToken)
        {
            var result = await _savedPollService.SaveAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Poll saved successfully"));
        }

        [HttpDelete("save-poll/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<SavedPollStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UnsavePoll(Guid id, CancellationToken cancellationToken)
        {
            var result = await _savedPollService.UnsaveAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Poll unsaved successfully"));
        }

        [HttpGet("view-poll/{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<UserPollDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetPollDetails(Guid id, CancellationToken cancellationToken)
        {
            var poll = await _userService.GetOwnedPollDetailsAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
        }

        [HttpPost("poll/create")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreatePoll(
            [FromBody] CreatePollRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _pollService.CreateAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken);

            return CreatedAtAction(
                nameof(PollsController.GetById),
                "Polls",
                new { id = result.Id },
                ApiResponse.Ok(result, "Poll created successfully"));
        }

        [HttpPost("close-poll/{id:guid}/close")]
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

        [HttpDelete("delete-poll/{id:guid}")]
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

        [HttpGet("notifications")]
        [ProducesResponseType(typeof(ApiResponse<List<NotificationResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _notificationService.GetMineAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Notifications retrieved successfully", result.Meta));
        }

        [HttpPatch("notifications/{id:guid}/read")]
        [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReadNotification(
            Guid id,
            CancellationToken cancellationToken)
        {
            var result = await _notificationService.MarkAsReadAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification marked as read"));
        }
    }
}
