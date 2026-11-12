using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>"Just for fun" cards: a system-made list with no live or expiry date that users make their own.</summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/fun-cards")]
    public class FunCardsController : ControllerBase
    {
        private readonly IFunCardService _service;

        private readonly ISavedFunCardService _saved;

        public FunCardsController(IFunCardService service, ISavedFunCardService saved)
        {
            _service = service;
            _saved = saved;
        }

        [AllowAnonymous]
        [HttpGet]
        [EndpointSummary("List Just For Fun Cards")]
        [EndpointDescription("Returns every active just-for-fun card, oldest first, with a used flag for the caller. These cards have no live or expiry date. Public.")]
        [ProducesResponseType(typeof(ApiResponse<List<FunCardResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            Guid? userId = User.TryGetUserId(out var id) ? id : null;
            var result = await _service.ListAsync(userId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Cards retrieved successfully"));
        }

        [HttpGet("saved")]
        [EndpointSummary("List Saved Fun Cards")]
        [EndpointDescription("Lists the just-for-fun cards the signed-in user saved, most recently saved first. Cursor pagination: pass the cursor from the previous response's meta. Returns 400 for an invalid cursor.")]
        [ProducesResponseType(typeof(ApiResponse<List<FunCardResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSaved([FromQuery] string? cursor, CancellationToken cancellationToken)
        {
            var result = await _saved.ListAsync(User.GetRequiredUserId(), cursor, cancellationToken);
            return Ok(ApiResponse.Ok(result.Items, "Saved cards retrieved successfully", result.Meta));
        }

        [HttpPut("{id:guid}/save")]
        [EndpointSummary("Save Fun Card")]
        [EndpointDescription("Adds a just-for-fun card to the signed-in user's saved list. Idempotent. Only active fun cards can be saved: weekly checks and personality cards cannot, and return 404.")]
        [ProducesResponseType(typeof(ApiResponse<SavedFunCardStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken)
        {
            var result = await _saved.SaveAsync(User.GetRequiredUserId(), id, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card saved successfully"));
        }

        [HttpDelete("{id:guid}/save")]
        [EndpointSummary("Unsave Fun Card")]
        [EndpointDescription("Removes a just-for-fun card from the signed-in user's saved list. Idempotent.")]
        [ProducesResponseType(typeof(ApiResponse<SavedFunCardStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Unsave(Guid id, CancellationToken cancellationToken)
        {
            var result = await _saved.UnsaveAsync(User.GetRequiredUserId(), id, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Card unsaved successfully"));
        }

        [HttpPost("make")]
        [EndpointSummary("Make a Just For Fun Card")]
        [EndpointDescription("Turns a just-for-fun card into the caller's own shareable card, once per card. The body carries the caller's own answer: exactly one of checkOptionId or customAnswer, plus an optional reason. The response is the same made-card shape as POST /checks/make. Requires sign-in.")]
        [ProducesResponseType(typeof(ApiResponse<UserCheckResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Make([FromBody] MakeCheckRequest request, CancellationToken cancellationToken)
        {
            var result = await _service.MakeAsync(User.GetRequiredUserId(), request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Card created successfully"));
        }
    }
}
