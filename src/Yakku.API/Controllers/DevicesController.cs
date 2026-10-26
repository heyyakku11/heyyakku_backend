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
    /// Devices of signed-in users. Every login session is linked to the device it runs on, so one user can be
    /// signed in on several devices at once and push goes only to devices whose session is still live.
    /// A Bearer JWT is required; guests do not register devices.
    /// </summary>
    [Authorize]
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
        [EndpointSummary("Register Device")]
        [EndpointDescription("DEPRECATED: send the device (installationId and platform) in the POST /api/v1/auth/verify request instead, so signing in and binding the device happen in one atomic call, then send the notification permission and push token with PATCH /api/v1/devices/me once the user has answered the OS prompt. Reason: registering in a separate call after sign-in leaves a window where the session exists but is not bound to its device, and it duplicates what verify now does. This route is kept only as a fallback for older app versions and for binding a device when verify could not (for example the device was omitted, or the installation still belonged to a guest session); it will be removed once those cases are gone. Registers the device the signed-in user is using and links the current login session to it, so this device receives the user's notifications for as long as that session is active. Call it after every sign-in and whenever the push token changes. The same installation never creates a second record; it is updated instead. The owner always comes from the access token, never from the body. Returns 201 when the device is new and 200 when it already existed. An installation or push token that belongs to another account returns 409.")]
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
            var caller = _callers.ResolveUser(HttpContext);
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
        [EndpointDescription("Partially updates the device linked to the caller's current login session: push-token rotation, device metadata, locale, timezone, app version and notification permission. Omitted fields are unchanged and an empty string clears a field (an empty pushToken stops push to this device). A denied permission clears the token. Returns 404 when the session has no registered device yet; call register first. The optional X-Installation-Id header selects one of the caller's own devices instead.")]
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
            var caller = _callers.ResolveUser(HttpContext);
            var device = await _deviceService.UpdateCurrentAsync(
                caller,
                DeviceCallerResolver.ReadInstallationId(HttpContext),
                request,
                cancellationToken);
            return Ok(ApiResponse.Ok(device, "Device updated successfully"));
        }
    }
}
