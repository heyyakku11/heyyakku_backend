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

        public FunCardsController(IFunCardService service)
        {
            _service = service;
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
