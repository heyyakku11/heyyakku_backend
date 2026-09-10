using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;

namespace Yakku.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/v1/devices")]
    public class DevicesController : ControllerBase
    {
        private readonly IDeviceService _deviceService;

        public DevicesController(IDeviceService deviceService)
        {
            _deviceService = deviceService;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Register(
            [FromBody] RegisterDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _deviceService.RegisterAsync(
                User.GetRequiredUserId(),
                request,
                cancellationToken);

            var response = ApiResponse.Ok(
                result.Device,
                result.Created ? "Device registered successfully" : "Device updated successfully");

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
        }

        [HttpPatch("{installationId}")]
        [ProducesResponseType(typeof(ApiResponse<DeviceResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(
            string installationId,
            [FromBody] UpdateDeviceRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _deviceService.UpdateAsync(
                User.GetRequiredUserId(),
                installationId,
                request,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Device updated successfully"));
        }

        [HttpDelete("{installationId}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unregister(
            string installationId,
            CancellationToken cancellationToken)
        {
            await _deviceService.UnregisterAsync(
                User.GetRequiredUserId(),
                installationId,
                cancellationToken);

            return Ok(ApiResponse.Ok<object?>(null, "Device unregistered successfully"));
        }
    }
}
