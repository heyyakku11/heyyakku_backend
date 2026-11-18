using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Entitlements;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// What a poll or check owner can do with Ask Back on a card, and unlocking it with a plan ticket.
    /// </summary>
    [Authorize]
    [ServiceFilter(typeof(LiveSessionFilter))]
    [ApiController]
    [Route("api/v1/ask-back")]
    public class AskBackAccessController : ControllerBase
    {
        private readonly IEntitlementService _entitlements;

        public AskBackAccessController(IEntitlementService entitlements)
        {
            _entitlements = entitlements;
        }

        [HttpGet("access")]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("Ask Back access for a card")]
        [EndpointDescription("Pass the voteId of any reason on the card. Says whether the card is unlocked (and how), whether the caller's plan can unlock it now, the tickets left, the questions allowed per reason and when the plan ends. Returns 404 unless the vote belongs to one of the caller's cards.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackAccessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAccess([FromQuery] Guid voteId, CancellationToken cancellationToken)
        {
            var result = await _entitlements.GetAccessAsync(User.GetRequiredUserId(), voteId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Access retrieved successfully"));
        }

        [HttpGet("card-access")]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("Ask Back access for a card, by card id")]
        [EndpointDescription("Same data as GET /access, named by the card instead of one of its votes: contentType is poll or check, contentId the poll id or the made check's id. reasonsRevealed false means the free plan: only each option's top reason is readable and the rest come back hidden. Returns 400 for another contentType and 404 unless the card is the caller's.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackAccessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCardAccess(
            [FromQuery] string? contentType,
            [FromQuery] Guid contentId,
            CancellationToken cancellationToken)
        {
            var result = await _entitlements.GetOwnedCardAccessAsync(
                User.GetRequiredUserId(), contentType, contentId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Access retrieved successfully"));
        }

        [HttpPost("unlocks")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Unlock a card with a plan ticket")]
        [EndpointDescription("Unlocks every reason on the card the vote belongs to, using one ticket of the caller's active plan. Asking again for a card that is already unlocked returns its access without spending another ticket. Returns 403 (ASK_BACK_UNLOCK_REQUIRED) when there is no active plan or no ticket left, 404 unless the vote belongs to one of the caller's cards.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackAccessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unlock([FromBody] UnlockCardRequest request, CancellationToken cancellationToken)
        {
            var result = await _entitlements.UnlockAsync(User.GetRequiredUserId(), request.VoteId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card unlocked"));
        }
    }
}
