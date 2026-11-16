using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.AdminReasonReports.DTOs;
using Yakku.Application.AdminReasonReports.Interfaces;
using Yakku.Application.AdminReasonReports.Services;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>Admin moderation of reasons that owners reported.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku/reason-reports")]
    public class AdminReasonReportsController : ControllerBase
    {
        private readonly IAdminReasonReportService _service;

        public AdminReasonReportsController(IAdminReasonReportService service)
        {
            _service = service;
        }

        [HttpGet]
        [EndpointSummary("List Reason Reports (Admin)")]
        [EndpointDescription("Lists the reasons owners reported, newest first: the reason text, the reason the reporter gave for reporting it, status (pending, approved or rejected), the reporter's anonymous name and when it was reported or reviewed. Paginated with page and pageSize (default 10 per page, max 50); meta has the total count and pages. Every status unless status is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminReasonReportResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AdminReasonReportService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListAsync(status, page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Reports retrieved successfully", meta));
        }

        [HttpPost("{id:guid}/approve")]
        [EndpointSummary("Approve A Reason Report (Admin)")]
        [EndpointDescription("Upholds a pending report. The reason text is removed from its vote, so the owner no longer sees it or counts it and only sees it in the blocked count; any Ask Back conversation about it is closed. 404 when the report does not exist, 409 when it was already approved or rejected. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        {
            await _service.ApproveAsync(id, cancellationToken);
            return Ok(ApiResponse.Ok<object?>(null, "Report approved"));
        }

        [HttpPost("{id:guid}/reject")]
        [EndpointSummary("Reject A Reason Report (Admin)")]
        [EndpointDescription("Dismisses a pending report. The reason stays visible to its owner and loses its reported flag; it cannot be reported again. 404 when the report does not exist, 409 when it was already approved or rejected. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
        {
            await _service.RejectAsync(id, cancellationToken);
            return Ok(ApiResponse.Ok<object?>(null, "Report rejected"));
        }
    }
}
