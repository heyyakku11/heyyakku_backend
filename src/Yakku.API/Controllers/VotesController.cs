using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Media;
using Yakku.Application.Common.Responses;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>User (JWT) voting for polls and checks — used by the mobile app.</summary>
    [Authorize]
    [ApiController]
    [Route("api/v1/votes")]
    public class VotesController : ControllerBase
    {
        private readonly IVotingService _voting;
        private readonly MediaCallerResolver _callerResolver;

        public VotesController(IVotingService voting, MediaCallerResolver callerResolver)
        {
            _voting = voting;
            _callerResolver = callerResolver;
        }

        [HttpPost]
        [EnableRateLimiting("poll-votes")]
        [EndpointSummary("Vote on a Poll or a Check (signed-in user)")]
        [EndpointDescription(
            "App-only voting. Requires a Bearer JWT. Send the content's shareCode (required) and pick exactly one of "
            + "optionId or customAnswer, with an optional reason. The backend works out whether the share code is a "
            + "poll or a check. Guest tokens are ignored. One vote per voter per content item (409 otherwise). "
            + "Returns the content as the voter now sees it. Web guests use POST /api/v1/guest/votes instead.")]
        [ProducesResponseType(typeof(ApiResponse<SharedContentResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Vote([FromBody] VoteRequest request, CancellationToken cancellationToken)
        {
            var voter = await _callerResolver.ResolveUserVoterAsync(HttpContext, cancellationToken);
            var result = await _voting.VoteAsync(request, voter, cancellationToken);
            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse.Ok(result, "Vote submitted successfully"));
        }
    }
}
