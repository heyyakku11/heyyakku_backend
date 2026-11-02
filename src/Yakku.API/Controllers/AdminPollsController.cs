using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.AdminPolls.DTOs;
using Yakku.Application.AdminPolls.Interfaces;
using Yakku.Application.AdminPolls.Services;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>Admin directory of polls.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/polls")]
    public class AdminPollsController : ControllerBase
    {
        private readonly IAdminPollService _service;

        public AdminPollsController(IAdminPollService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("List Polls (Admin)")]
        [EndpointDescription("Lists polls newest first: question, type, created, expiry, status, creator anonymous name, vote count and reason count. Paginated with page and pageSize (default 10 per page, max 50); meta has the total count and pages. Every status unless status (draft, active, closed or expired) is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AdminPollService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListAsync(status, page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Polls retrieved successfully", meta));
        }
    }
}
