using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/checks")]
    public class ChecksController : ControllerBase
    {
        private readonly ICheckService _checkService;

        public ChecksController(ICheckService checkService)
        {
            _checkService = checkService;
        }

        /// <summary>Active checks for the current week (Monday to Sunday), with the caller's used flag. Public.</summary>
        [AllowAnonymous]
        [HttpGet]
        [EndpointSummary("List Current Checks")]
        [EndpointDescription("Returns the active checks for the current week (Monday to Sunday), with a used flag for the caller. Public.")]
        [ProducesResponseType(typeof(ApiResponse<List<CheckResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var result = await _checkService.GetChecksAsync(CurrentUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Checks retrieved successfully"));
        }

        /// <summary>
        /// Makes a live check (current or past week) into the caller's own shareable check, once per check.
        /// The body carries the caller's own answer: exactly one of checkOptionId or customAnswer, plus an optional reason.
        /// A bearer token is required.
        /// </summary>
        [HttpPost("make")]
        [EndpointSummary("Make a Check")]
        [EndpointDescription("Turns a live check into the caller's own shareable check, once per check. Requires sign-in.")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Make(
            [FromBody] MakeCheckRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _checkService.MakeAsync(User.GetRequiredUserId(), request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Check created successfully"));
        }

        /// <summary>The caller's made checks, kept separate from their polls.</summary>
        [HttpGet("mine")]
        [EndpointSummary("List My Checks")]
        [EndpointDescription("Returns the checks the signed-in user has made, kept separate from their polls.")]
        [ProducesResponseType(typeof(ApiResponse<List<UserCheckResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
        {
            var result = await _checkService.GetMineAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Your checks retrieved successfully"));
        }

        /// <summary>Closes one of the caller's made checks so it stops taking answers.</summary>
        [HttpPost("mine/{id:guid}/close")]
        [EndpointSummary("Close My Check")]
        [EndpointDescription("Closes a made check the signed-in user owns so it stops accepting answers. Existing answers are kept. Returns 404 when it does not exist or is not yours, and 409 when it is already closed.")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CloseMine(Guid id, CancellationToken cancellationToken)
        {
            var result = await _checkService.CloseMineAsync(id, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Check closed successfully"));
        }

        /// <summary>Permanently deletes one of the caller's made checks with its answers.</summary>
        [HttpDelete("mine/{id:guid}")]
        [EndpointSummary("Delete My Check")]
        [EndpointDescription("Permanently deletes a made check the signed-in user owns, together with its answers. Returns 404 when it does not exist or is not yours.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMine(Guid id, CancellationToken cancellationToken)
        {
            await _checkService.DeleteMineAsync(id, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok<object?>(null, "Check deleted successfully"));
        }

        /// <summary>The signed-in user's id, or null for anonymous callers on the public routes.</summary>
        private Guid? CurrentUserId()
        {
            return User.TryGetUserId(out var userId) ? userId : null;
        }
    }
}
