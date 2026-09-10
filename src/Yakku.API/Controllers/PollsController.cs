using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.API.Guests;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/v1/polls")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _pollService;
        private readonly IVoteService _voteService;
        private readonly GuestCookieService _guestCookieService;

        public PollsController(
            IPollService pollService,
            IVoteService voteService,
            GuestCookieService guestCookieService)
        {
            _pollService = pollService;
            _voteService = voteService;
            _guestCookieService = guestCookieService;
        }

        [Authorize]
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Create(
            [FromBody] CreatePollRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _pollService.CreateAsync(
                request,
                User.GetRequiredUserId(),
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                ApiResponse.Ok(result, "Poll created successfully"));
        }

        [Authorize]
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<PollSummaryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMine(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _pollService.GetCreatorPollsAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [Authorize]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var poll = await _pollService.GetPollDetailsAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
        }

        [Authorize]
        [HttpPost("{id:guid}/close")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
        {
            var poll = await _pollService.ClosePollAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok(poll, "Poll closed successfully"));
        }

        [Authorize]
        [HttpDelete("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _pollService.DeletePollAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok<object?>(null, "Poll deleted successfully"));
        }

        [AllowAnonymous]
        [HttpPost("{id:guid}/votes")]
        [ProducesResponseType(typeof(ApiResponse<VoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Vote(
            Guid id,
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            Guid? userId = null;
            Guid? guestId = null;

            if (User.TryGetUserId(out var authenticatedUserId))
            {
                userId = authenticatedUserId;
            }
            else
            {
                guestId = await _guestCookieService.EnsureAsync(HttpContext, cancellationToken);
            }

            var result = await _voteService.CastAsync(
                id,
                userId,
                guestId,
                request,
                cancellationToken);
            return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
        }
    }
}
