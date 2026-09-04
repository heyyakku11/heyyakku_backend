using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/system")]
    public class SystemController : ControllerBase
    {
        private readonly ISystemService _systemService;
        private readonly ISystemHealthService _systemHealthService;

        public SystemController(ISystemService systemService, ISystemHealthService systemHealthService)
        {
            _systemService = systemService;
            _systemHealthService = systemHealthService;
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

        [HttpPost("otp/decrypt")]
        [ProducesResponseType(typeof(ApiResponse<DecryptOtpResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DecryptOtp(
            [FromBody] DecryptOtpRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _systemService.DecryptOtpAsync(request, cancellationToken);
                return Ok(ApiResponse.Ok(result, "OTP decrypted successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        private static ObjectResult ToErrorResult(AppException exception)
        {
            var response = ApiResponse.Fail(
                exception.Message,
                [
                    new ApiError
                    {
                        Code = exception.ErrorCode,
                        Message = exception.ErrorMessage,
                        Field = exception.Field
                    }
                ]);

            return new ObjectResult(response)
            {
                StatusCode = exception.StatusCode
            };
        }
    }
}
