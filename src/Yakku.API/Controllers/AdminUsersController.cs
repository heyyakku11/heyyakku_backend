using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.AdminUsers.DTOs;
using Yakku.Application.AdminUsers.Interfaces;
using Yakku.Application.AdminUsers.Services;
using Yakku.Application.Common.Responses;
using Yakku.Application.Entitlements;
using Yakku.Application.Plans.DTOs;
using Yakku.Application.Plans.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>Admin directory of app users and guests.</summary>
    [ApiController]
    [Authorize(Policy = "Admin")]
    [Route("api/v1/yakku")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _service;

        private readonly IEntitlementService _entitlements;
        private readonly IPlanService _plans;

        public AdminUsersController(IAdminUserService service, IEntitlementService entitlements, IPlanService plans)
        {
            _service = service;
            _entitlements = entitlements;
            _plans = plans;
        }

        [HttpPatch("plans/{planId:guid}")]
        [EndpointSummary("Update a Plan (Admin)")]
        [EndpointDescription("Changes only the fields that are sent: name, priceMinor, isActive, durationDays (1 or more), unlockTickets (0 or more, -1 for unlimited) and askBackMessageLimit (1 or more, -1 for unlimited). Changes to the Ask Back message limit apply to existing conversations too. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<PlanResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdatePlan(
            Guid planId,
            [FromBody] UpdatePlanRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _plans.UpdateAsync(planId, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Plan updated"));
        }

        [HttpPost("users/{userId:guid}/plan")]
        [EndpointSummary("Activate a Plan for a User (Admin)")]
        [EndpointDescription("Puts the user on the plan (free, yakku_lite or yakku_love) starting now. A paid plan runs for the plan's duration and brings its unlock tickets; activating the same plan again while it is active restarts the period and resets the tickets. This is the hook a payment provider will call once one is integrated. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<ActivatePlanResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ActivatePlan(
            Guid userId,
            [FromBody] ActivatePlanRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _entitlements.ActivatePlanAsync(userId, request.PlanCode ?? string.Empty, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Plan activated"));
        }

        [HttpPost("users/{userId:guid}/card-unlocks")]
        [EndpointSummary("Unlock a Card for a User (Admin)")]
        [EndpointDescription("Unlocks a poll or check (contentType poll or check, plus its id) for its owner as a single-card purchase: every reason on it can be asked back, it never expires and it is not affected by plan expiry. This is the hook a payment provider will call for a one-card purchase. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<GrantCardUnlockResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GrantCardUnlock(
            Guid userId,
            [FromBody] GrantCardUnlockRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _entitlements.GrantCardUnlockAsync(
                userId, request.ContentType, request.ContentId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card unlocked"));
        }

        [HttpGet("users")]
        [EndpointSummary("List Users (Admin)")]
        [EndpointDescription("Lists users newest first: anonymous name, phone with country code, plan, status, avatar and last login. Paginated with page and pageSize (default 10 per page, max 50); meta has the total count and pages. Every status unless status (active or disabled) is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminUserResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListUsers(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AdminUserService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListUsersAsync(status, page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Users retrieved successfully", meta));
        }

        [HttpGet("guests")]
        [EndpointSummary("List Guests (Admin)")]
        [EndpointDescription("Lists guests newest first: created, status, device, expiry, last seen and card votes. Paginated with page and pageSize (default 10 per page, max 50); meta has the total count and pages. Every status unless status (active or expired) is given. Admin only.")]
        [ProducesResponseType(typeof(ApiResponse<List<AdminGuestResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListGuests(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AdminUserService.DefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _service.ListGuestsAsync(status, page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Guests retrieved successfully", meta));
        }
    }
}
