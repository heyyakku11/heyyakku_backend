using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>Admin scheduling of checks: which checks go live in which week.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/checks")]
    public class AdminChecksController : ControllerBase
    {
        private readonly IAdminCheckService _service;

        public AdminChecksController(IAdminCheckService service)
        {
            _service = service;
        }

        /// <summary>All checks, earliest week first. Pass weekStart (any date in the week) to see one week.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<AdminCheckResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> List([FromQuery] DateOnly? weekStart, CancellationToken cancellationToken)
        {
            var result = await _service.ListAsync(weekStart, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Checks retrieved successfully"));
        }

        /// <summary>Schedules a check to go live in the week containing weekStart (current or a future week).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<AdminCheckResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create([FromBody] SaveCheckRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.CreateAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Check scheduled successfully"));
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<AdminCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(Guid id, [FromBody] SaveCheckRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.UpdateAsync(id, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Check updated successfully"));
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _service.DeleteAsync(id, cancellationToken);
            return NoContent();
        }
    }
}
