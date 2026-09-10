using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.NotificationPreferences.DTOs;
using Yakku.Application.NotificationPreferences.Interfaces;

namespace Yakku.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/notification-preferences")]
    public class NotificationPreferencesController : ControllerBase
    {
        private readonly INotificationPreferenceService _preferenceService;

        public NotificationPreferencesController(INotificationPreferenceService preferenceService)
        {
            _preferenceService = preferenceService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var result = await _preferenceService.GetAsync(
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification preferences retrieved successfully"));
        }

        [HttpPatch]
        [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Update(
            [FromBody] UpdateNotificationPreferenceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _preferenceService.UpdateAsync(
                User.GetRequiredUserId(),
                request,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification preferences updated successfully"));
        }
    }
}
