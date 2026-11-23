using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.AppVersion.DTOs;
using Yakku.Application.AppVersion.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/app")]
    public class AppController : ControllerBase
    {
        private readonly IAppVersionService _appVersionService;

        public AppController(IAppVersionService appVersionService)
        {
            _appVersionService = appVersionService;
        }

        [HttpGet("version")]
        [EndpointSummary("Check app version")]
        [EndpointDescription(
            "Public launch check for mobile clients. Pass platform (android|ios) and the installed semver version. " +
            "Returns whether a soft update or force update is required, plus the store URL and message. " +
            "forceUpdate when current is below minSupported; updateRequired (soft) when current is below latest but still supported.")]
        [ProducesResponseType(typeof(ApiResponse<AppVersionCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CheckVersion(
            [FromQuery] string? platform,
            [FromQuery] string? version,
            CancellationToken cancellationToken)
        {
            var result = await _appVersionService.CheckAsync(
                new AppVersionCheckRequest
                {
                    Platform = platform,
                    Version = version
                },
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "OK"));
        }
    }
}
