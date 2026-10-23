using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.NotificationPreferences.DTOs;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>The signed-in user's in-app notifications and the preferences that control which ones they receive.</summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly INotificationPreferenceService _preferenceService;

        public NotificationsController(
            INotificationService notificationService,
            INotificationPreferenceService preferenceService)
        {
            _notificationService = notificationService;
            _preferenceService = preferenceService;
        }

        [HttpGet]
        [EndpointSummary("List My Notifications")]
        [EndpointDescription("Lists the signed-in user's notifications, newest first. Uses cursor pagination: pass the cursor from the previous response's meta to get the next page. Returns 400 for an invalid cursor.")]
        [ProducesResponseType(typeof(ApiResponse<List<NotificationResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _notificationService.GetMineAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Notifications retrieved successfully", result.Meta));
        }

        [HttpPatch("{id:guid}/read")]
        [EndpointSummary("Mark Notification Read")]
        [EndpointDescription("Marks one of the signed-in user's notifications as read and returns it. Returns 404 when the notification does not exist or belongs to someone else.")]
        [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
        {
            var result = await _notificationService.MarkAsReadAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification marked as read"));
        }

        [HttpGet("preferences")]
        [EndpointSummary("Get Notification Preferences")]
        [EndpointDescription("Returns the signed-in user's notification preferences: pushEnabled (master switch for push), pollActivityEnabled (activity on their polls) and offersEnabled (promotions). Defaults are created on first read.")]
        [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
        {
            var result = await _preferenceService.GetAsync(
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification preferences retrieved successfully"));
        }

        [HttpPatch("preferences")]
        [EndpointSummary("Update Notification Preferences")]
        [EndpointDescription("Partially updates the signed-in user's notification preferences. Send only the flags to change (pushEnabled, pollActivityEnabled, offersEnabled); omitted flags keep their current value. Returns the updated preferences.")]
        [ProducesResponseType(typeof(ApiResponse<NotificationPreferenceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> UpdatePreferences(
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
