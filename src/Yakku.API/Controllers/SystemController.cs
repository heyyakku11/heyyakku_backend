using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Responses;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/system")]
    public class SystemController : ControllerBase
    {
        private readonly ISystemHealthService _systemHealthService;

        public SystemController(ISystemHealthService systemHealthService)
        {
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
    }
}
