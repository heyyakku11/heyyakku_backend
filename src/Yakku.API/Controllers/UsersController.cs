using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Streaks.DTOs;
using Yakku.Application.Streaks.Interfaces;
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
        private readonly IAccountDeletionService _accountDeletionService;
        private readonly IStreakService _streakService;

        public UsersController(
            IUserService userService,
            IAccountDeletionService accountDeletionService,
            IStreakService streakService)
        {
            _accountDeletionService = accountDeletionService;
            _streakService = streakService;
            _userService = userService;
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

        [HttpGet("streak")]
        [EndpointSummary("Get My Streak")]
        [EndpointDescription("Returns the signed-in user's daily-activity streak. Streak days are UTC calendar days earned by creating a poll or voting. currentStreak is zero once a whole day has been missed; longestStreak is never reduced.")]
        [ProducesResponseType(typeof(ApiResponse<StreakResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStreak(CancellationToken cancellationToken)
        {
            var streak = await _streakService.GetAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(streak, "Streak retrieved successfully"));
        }

        [HttpGet("streak/history")]
        [EndpointSummary("Get My Streak History")]
        [EndpointDescription("Lists the UTC dates on which the signed-in user earned a streak day, within from..to (inclusive, at most 366 days; defaults to the last 30 days).")]
        [ProducesResponseType(typeof(ApiResponse<StreakHistoryResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetStreakHistory(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var history = await _streakService.GetHistoryAsync(
                User.GetRequiredUserId(), from, to, cancellationToken);
            return Ok(ApiResponse.Ok(history, "Streak history retrieved successfully"));
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
    }
}
