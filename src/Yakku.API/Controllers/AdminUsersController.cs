using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.AdminUsers.DTOs;
using Yakku.Application.AdminUsers.Interfaces;
using Yakku.Application.AdminUsers.Services;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>Admin directory of app users and guests.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _service;

        public AdminUsersController(IAdminUserService service)
        {
            _service = service;
        }

        [HttpGet("users")]
        [EndpointSummary("List Users (Admin)")]
        [EndpointDescription("Lists users newest first: anonymous name, phone with country code, plan, status, avatar and last login. Cursor-paginated (default 10 per page, max 50): pass the previous response nextCursor as cursor. Every status unless status (active or disabled) is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminUserResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListUsers(
            [FromQuery] string? status,
            [FromQuery] string? cursor = null,
            [FromQuery] int pageSize = AdminUserService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListUsersAsync(status, cursor, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Users retrieved successfully", meta));
        }

        [HttpGet("guests")]
        [EndpointSummary("List Guests (Admin)")]
        [EndpointDescription("Lists guests newest first: created, status, device, expiry, last seen and card votes. Cursor-paginated (default 10 per page, max 50): pass the previous response nextCursor as cursor. Every status unless status (active or expired) is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminGuestResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListGuests(
            [FromQuery] string? status,
            [FromQuery] string? cursor = null,
            [FromQuery] int pageSize = AdminUserService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListGuestsAsync(status, cursor, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Guests retrieved successfully", meta));
        }
    }
}
