using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
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

        public UsersController(
            IUserService userService,
            IAccountDeletionService accountDeletionService)
        {
            _accountDeletionService = accountDeletionService;
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

        [HttpPatch("push")]
        [EndpointSummary("Set Push Notifications")]
        [EndpointDescription("Turns push notifications on or off for the signed-in user (isPushAllowed) and returns the updated profile.")]
        [ProducesResponseType(typeof(ApiResponse<UserProfileResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SetPushAllowed(
            [FromBody] UpdatePushAllowedRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _userService.SetPushAllowedAsync(
                User.GetRequiredUserId(), request.IsPushAllowed, cancellationToken);
            return user is null
                ? NotFound(ApiResponse.NotFound<UserProfileResponse>("User not found"))
                : Ok(ApiResponse.Ok(user, "Push notifications updated successfully"));
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
