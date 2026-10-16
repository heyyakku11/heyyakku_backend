using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Devices;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Device lifecycle. Register / update / unregister work for a signed-in user (Bearer JWT) or a guest
    /// (X-Guest-Token header or cookie). The current installation is the one linked to the caller's session, or the
    /// one named by the X-Installation-Id header (required for guests).
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/devices")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;
        private readonly DeviceCallerResolver _callers;

        public DevicesController(IDeviceService deviceService, DeviceCallerResolver callers)
        {
            _deviceService = deviceService;
            _callers = callers;
        }

        [HttpPost("register")]
        [EnableRateLimiting("device-write")]
        [EndpointSummary("Register or Upsert Device")]
        [EndpointDescription("Registers the caller's installation (guest or signed-in user), or updates the existing record for the same installation. Ownership comes from the credential, never the body. An installation or push token owned by someone else returns 409.")]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var caller = await _callers.ResolveAsync(HttpContext, cancellationToken);
            var result = await _deviceService.RegisterAsync(caller, request, cancellationToken);

            var response = ApiResponse.Ok(
                result.Device,
                result.Created ? "Device registered successfully" : "Device updated successfully");

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
        }

        [HttpPatch("me")]
        [EnableRateLimiting("device-write")]
        [EndpointSummary("Update Current Device")]
        [EndpointDescription("Partial update of the caller's current installation: push-token rotation, metadata, locale, timezone, app version, notification permission. Omitted fields are unchanged; an empty string clears a field (an empty pushToken disables push). Denied permission clears the token. Identify the installation with X-Installation-Id (required for guests).")]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> UpdateCurrent(
            [FromBody] UpdateDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var caller = await _callers.ResolveAsync(HttpContext, cancellationToken);
            var device = await _deviceService.UpdateCurrentAsync(
                caller,
                DeviceCallerResolver.ReadInstallationId(HttpContext),
                request,
                cancellationToken);
            return Ok(ApiResponse.Ok(device, "Device updated successfully"));
        }

        [HttpDelete("me")]
        [EnableRateLimiting("device-write")]
        [EndpointSummary("Unregister Current Device")]
        [EndpointDescription("Stops push delivery to the caller's current installation and clears its push token. The record is retained, login sessions are not revoked, and repeating the call is safe (204).")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> UnregisterCurrent(CancellationToken cancellationToken)
        {
            var caller = await _callers.ResolveAsync(HttpContext, cancellationToken);
            await _deviceService.UnregisterCurrentAsync(
                caller,
                DeviceCallerResolver.ReadInstallationId(HttpContext),
                cancellationToken);
            return NoContent();
        }

        [Authorize]
        [HttpGet("me")]
        [EnableRateLimiting("device-read")]
        [EndpointSummary("Get Current User's Devices")]
        [EndpointDescription("Lists the authenticated user's own installations. Push tokens are never returned; pushEnabled reports whether delivery is currently possible. isCurrent marks the installation named by X-Installation-Id or linked to this session.")]
        [ProducesResponseType(typeof(ApiResponse<List<DeviceResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            var caller = _callers.ResolveUser(HttpContext);
            var devices = await _deviceService.ListAsync(
                caller,
                DeviceCallerResolver.ReadInstallationId(HttpContext),
                cancellationToken);
            return Ok(ApiResponse.Ok(devices, "Devices retrieved successfully"));
        }
    }
}
