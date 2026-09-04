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
    [Route("api/polls")]
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

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            await _guestCookieService.EnsureAsync(HttpContext, cancellationToken);
            var poll = await _pollService.GetByIdAsync(id, cancellationToken);
            return poll is null
                ? NotFound(ApiResponse.NotFound<PollResponse>("Poll not found"))
                : Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
        }

        [HttpPost("{id:guid}/votes")]
        public async Task<IActionResult> Vote(
            Guid id,
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            var guestId = await _guestCookieService.EnsureAsync(HttpContext, cancellationToken);
            var result = await _voteService.CastAsync(id, guestId, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
        }
    }
}
