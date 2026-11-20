using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.AskBack.Services;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>People the signed-in user blocked from Ask Back, and lifting those blocks from Settings.</summary>
    [Authorize]
    [ServiceFilter(typeof(LiveSessionFilter))]
    [ApiController]
    [Route("api/v1/ask-back/blocks")]
    public class AskBackBlocksController : ControllerBase
    {
        private readonly IAskBackService _askBack;

        public AskBackBlocksController(IAskBackService askBack)
        {
            _askBack = askBack;
        }

        [HttpGet]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("List people I blocked")]
        [EndpointDescription(
            "Returns people the caller blocked from Ask Back, newest first. Each row has an anonymous name only. " +
            "Paginated with page and pageSize (default 20, max 50).")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AskBackBlockItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> List(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = AskBackService.BlocksDefaultPageSize,
            CancellationToken cancellationToken = default)
        {
            var (items, meta) = await _askBack.ListBlocksAsync(
                User.GetRequiredUserId(), page, pageSize, cancellationToken);
            return Ok(ApiResponse.Ok(items, "Blocked people", meta));
        }

        [HttpDelete("{blockId:guid}")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Unblock a person")]
        [EndpointDescription(
            "Lifts a block the caller owns. Also reactivates Ask Back conversations the caller blocked with that person. " +
            "404 when the block is missing or not the caller's.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Unblock(Guid blockId, CancellationToken cancellationToken)
        {
            await _askBack.UnblockByBlockIdAsync(User.GetRequiredUserId(), blockId, cancellationToken);
            return Ok(ApiResponse.Ok<object?>(null, "Block lifted"));
        }
    }
}
