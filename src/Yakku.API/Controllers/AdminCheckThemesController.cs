using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Admin definition of check themes. A theme belongs to a check type; admins then pick the theme code when
    /// creating a check, and the backend applies the theme. Clients never define visuals themselves.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/check-themes")]
    public class AdminCheckThemesController : ControllerBase
    {
        private readonly ICheckThemeService _themes;

        public AdminCheckThemesController(ICheckThemeService themes)
        {
            _themes = themes;
        }

        [HttpGet]
        [EndpointSummary("List Check Themes (Admin)")]
        [EndpointDescription("Lists every check theme (active and inactive) in display order. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<CheckThemeDefinitionResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List(CancellationToken cancellationToken)
        {
            var result = await _themes.ListAsync(cancellationToken);
            return Ok(ApiResponse.Ok(result, "Check themes retrieved successfully"));
        }

        [HttpPost]
        [EndpointSummary("Create Check Theme (Admin)")]
        [EndpointDescription("Defines a new check theme and the check type it belongs to. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<CheckThemeDefinitionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] CreateCheckThemeRequest request, CancellationToken cancellationToken)
        {
            var result = await _themes.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Check theme created successfully"));
        }
    }
}
