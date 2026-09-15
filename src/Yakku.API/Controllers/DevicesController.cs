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
                User.GetRequiredSessionId(),
                cancellationToken);

            var response = ApiResponse.Ok(
                result.Device,
                result.Created ? "Device registered successfully" : "Device updated successfully");

            return result.Created
                ? StatusCode(StatusCodes.Status201Created, response)
                : Ok(response);
        }
    }
}
