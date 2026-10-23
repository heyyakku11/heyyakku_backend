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
        private readonly IAccountDeletionService _accountDeletionService;

        public UsersController(
            IUserService userService,
            IPollService pollService,
            ISavedPollService savedPollService,
            INotificationService notificationService,
            IAccountDeletionService accountDeletionService)
        {
            _accountDeletionService = accountDeletionService;
            _userService = userService;
            _pollService = pollService;
            _savedPollService = savedPollService;
            _notificationService = notificationService;
        }

        [HttpGet()]
        [EndpointSummary("Get Current User")]
        [EndpointDescription("Returns the signed-in user's profile.")]
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

        /// <summary>
        /// Schedules the account for permanent deletion and signs the user out everywhere. Logging in again
        /// before the returned date cancels the deletion.
        /// </summary>
        [HttpDelete("me")]
        [EndpointSummary("Delete My Account")]
        [EndpointDescription("Schedules the signed-in user's account for permanent deletion and signs them out everywhere.")]
        [ProducesResponseType(typeof(ApiResponse<AccountDeletionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMe(CancellationToken cancellationToken)
        {
            var result = await _accountDeletionService.RequestDeletionAsync(
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Account scheduled for deletion"));
        }

        [HttpGet("asked-polls")]
        [EndpointSummary("List My Asked Polls")]
        [EndpointDescription("Returns the polls the signed-in user created, using cursor pagination.")]
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
        [EndpointSummary("List My Answered Polls")]
        [EndpointDescription("Returns the polls the signed-in user has voted on, using cursor pagination.")]
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
        [EndpointSummary("List Saved Polls")]
        [EndpointDescription("Returns the polls the signed-in user has saved, using cursor pagination.")]
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
        [EndpointSummary("Save Poll")]
        [EndpointDescription("Adds a poll to the signed-in user's saved polls.")]
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
        [EndpointSummary("Unsave Poll")]
        [EndpointDescription("Removes a poll from the signed-in user's saved polls.")]
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
        [EndpointSummary("View My Poll Details")]
        [EndpointDescription("Returns details and results for a poll the signed-in user owns.")]
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
        [EndpointSummary("Create Poll (Current User)")]
        [EndpointDescription("Creates a poll owned by the signed-in user.")]
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
        [EndpointSummary("Close Poll")]
        [EndpointDescription("Closes a poll owned by the signed-in user so it stops accepting votes.")]
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
        [EndpointSummary("Delete My Poll")]
        [EndpointDescription("Deletes a poll owned by the signed-in user.")]
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
        [EndpointSummary("List My Notifications")]
        [EndpointDescription("Returns the signed-in user's notifications, using cursor pagination.")]
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
        [EndpointSummary("Mark Notification Read")]
        [EndpointDescription("Marks one of the signed-in user's notifications as read.")]
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
