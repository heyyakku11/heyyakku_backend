using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Responses;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/v1/system")]
    public class SystemController : ControllerBase
    {
        private readonly ISystemHealthService _systemHealthService;
        private readonly IPushNotificationService _pushNotificationService;

        public SystemController(
            ISystemHealthService systemHealthService,
            IPushNotificationService pushNotificationService)
        {
            _systemHealthService = systemHealthService;
            _pushNotificationService = pushNotificationService;
        }

        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<SystemHealthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<SystemHealthResponse>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
        {
            var result = await _systemHealthService.GetHealthAsync(cancellationToken);
            var healthy = result.Status == "Healthy";
            var response = new ApiResponse<SystemHealthResponse>
            {
                Success = healthy,
                Message = healthy ? "System is healthy" : "System is unhealthy",
                Data = result
            };

            return healthy
                ? Ok(response)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
        }

        [HttpPost("test-notification/device")]
        [ProducesResponseType(typeof(ApiResponse<PushDeviceNotificationResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendTestNotificationToDevice(
            [FromBody] TestDeviceNotificationRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _pushNotificationService.SendTestToDeviceAsync(
                request,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Test notification sent to device"));
        }

        [HttpPost("test-notification/user")]
        [ProducesResponseType(typeof(ApiResponse<PushUserNotificationResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> SendTestNotificationToUser(
            [FromBody] TestUserNotificationRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _pushNotificationService.SendTestToUserAsync(
                request,
                cancellationToken);

            var message = result.Failed == 0
                ? "Test notification sent to user devices"
                : "Test notification completed with partial failures";

            return Ok(ApiResponse.Ok(result, message));
        }
    }
}
