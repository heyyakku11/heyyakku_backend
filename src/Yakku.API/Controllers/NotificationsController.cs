using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>The signed-in user's in-app notifications.</summary>
    [Authorize]
    [ServiceFilter(typeof(LiveSessionFilter))]
    [ApiController]
    [Route("api/v1/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
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

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get Notification")]
        [EndpointDescription("Returns one of the signed-in user's notifications; this is where a push notification's notificationId is opened. Returns 404 when it does not exist or belongs to someone else.")]
        [ProducesResponseType(typeof(ApiResponse<NotificationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
        {
            var result = await _notificationService.GetAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Notification retrieved successfully"));
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
    }
}
