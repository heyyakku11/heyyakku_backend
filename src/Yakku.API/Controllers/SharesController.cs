using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Media;
using Yakku.Application.Common.Responses;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Universal share links. A share code resolves to a poll or a made check; both come back in one shape.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/shares")]
    public class SharesController : ControllerBase
    {
        private readonly ISharedContentService _sharedContent;
        private readonly MediaCallerResolver _callerResolver;

        public SharesController(ISharedContentService sharedContent, MediaCallerResolver callerResolver)
        {
            _sharedContent = sharedContent;
            _callerResolver = callerResolver;
        }

        [HttpGet("{shareCode}/result")]
        [EndpointSummary("Get Shared Result")]
        [EndpointDescription("The shareable result of a card, for the result link. Percentages only (no counts, names or reasons). Options are sent only once the card has ended with enough votes (resultsAvailable); while it is live, isLive is true and the link should send people to vote instead. Works for closed and expired cards. Anonymous.")]
        [ProducesResponseType(typeof(ApiResponse<SharedResultResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetResult(string shareCode, CancellationToken cancellationToken)
        {
            var result = await _sharedContent.GetResultAsync(shareCode, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Result retrieved successfully"));
        }

        [HttpGet("{shareCode}")]
        [EndpointSummary("Get Shared Content")]
        [EndpointDescription("Resolves a share code to a poll or a check (contentType POLL or CHECK). Returns the question, options, the caller's voting state and the permitted statistics. Anonymous callers are allowed; send a Bearer JWT or an X-Guest-Token to get hasVoted/myVote. Counts and percentages are included only when resultsVisible is true.")]
        [ProducesResponseType(typeof(ApiResponse<SharedContentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string shareCode, CancellationToken cancellationToken)
        {
            var viewer = await _callerResolver.TryResolveVoterAsync(HttpContext, cancellationToken);
            var result = await _sharedContent.GetAsync(shareCode, viewer, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Content retrieved successfully"));
        }
    }
}
