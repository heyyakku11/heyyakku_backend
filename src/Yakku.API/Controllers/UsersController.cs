using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly IAnonymousNameService _anonymousNameService;
        private readonly IUserSettingsService _settingsService;

        public UsersController(
            IUserService userService,
            IAccountDeletionService accountDeletionService,
            IStreakService streakService,
            IAnonymousNameService anonymousNameService,
            IUserSettingsService settingsService)
        {
            _anonymousNameService = anonymousNameService;
            _settingsService = settingsService;
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

        [HttpGet("me/settings")]
        [EndpointSummary("Get My Settings")]
        [EndpointDescription("Returns the signed-in user's account settings: allowAskBack (whether people they answer may ask them back). Push notifications are per device and live under /devices.")]
        [ProducesResponseType(typeof(ApiResponse<UserSettingsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
        {
            var settings = await _settingsService.GetAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(settings, "Settings retrieved successfully"));
        }

        [HttpPatch("me/settings")]
        [EndpointSummary("Update My Settings")]
        [EndpointDescription("Changes the settings that are sent (currently allowAskBack) and returns all of them. Independent of the device's push setting.")]
        [ProducesResponseType(typeof(ApiResponse<UserSettingsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSettings(
            [FromBody] UpdateUserSettingsRequest request,
            CancellationToken cancellationToken)
        {
            var settings = await _settingsService.UpdateAsync(User.GetRequiredUserId(), request, cancellationToken);
            return Ok(ApiResponse.Ok(settings, "Settings updated successfully"));
        }

        [HttpPost("me/anonymous-name")]
        [EnableRateLimiting("anonymous-name-regenerate")]
        [EndpointSummary("Regenerate My Anonymous Name")]
        [EndpointDescription("Gives the signed-in user a new random, unique anonymous name, saves it to their profile and returns it. The user comes from the access token; there is no request body. Limited to 10 calls per minute per user.")]
        [ProducesResponseType(typeof(ApiResponse<AnonymousNameResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> RegenerateAnonymousName(CancellationToken cancellationToken)
        {
            var result = await _anonymousNameService.RegenerateAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Anonymous name updated successfully"));
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
