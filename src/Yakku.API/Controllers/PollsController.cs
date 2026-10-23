using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.API.Media;
using Yakku.API.Middleware;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/polls")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _pollService;
        private readonly IVoteService _voteService;
        private readonly MediaCallerResolver _callerResolver;

        public PollsController(IPollService pollService, IVoteService voteService, MediaCallerResolver callerResolver)
        {
            _pollService = pollService;
            _voteService = voteService;
            _callerResolver = callerResolver;
        }

        [AllowAnonymous]
        [HttpGet]
        [EndpointSummary("List Polls")]
        [EndpointDescription("Returns public polls, newest first, using cursor pagination. Optional contentType filter. Public.")]
        [ProducesResponseType(typeof(ApiResponse<List<PollResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? cursor,
            [FromQuery] string? contentType,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _pollService.GetPollsAsync(cursor, contentType, cancellationToken);
                return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        [EndpointSummary("Get Poll")]
        [EndpointDescription("Returns one poll with its options and vote counts. Public polls are readable without signing in.")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var poll = await _pollService.GetPollDetailsAsync(
                    id,
                    CurrentUserId(),
                    cancellationToken);
                return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        /// <summary>Loads a poll by the share code carried in shared links. Same visibility rules as get-by-id.</summary>
        [AllowAnonymous]
        [HttpGet("share/{shareCode}")]
        [EndpointSummary("Get Poll by Share Code")]
        [EndpointDescription("Returns a poll using the share code carried in shared links. Same visibility rules as get-by-id.")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByShareCode(string shareCode, CancellationToken cancellationToken)
        {
            try
            {
                var poll = await _pollService.GetPollByShareCodeAsync(
                    shareCode,
                    CurrentUserId(),
                    cancellationToken);
                return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}/results")]
        [EndpointSummary("Get Poll Results")]
        [EndpointDescription("Returns per-option vote counts for a poll. Counts stay hidden until enough votes from others are in.")]
        [ProducesResponseType(typeof(ApiResponse<PollResultsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetResults(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var results = await _pollService.GetResultsAsync(id, CurrentUserId(), cancellationToken);
                return Ok(ApiResponse.Ok(results, "Poll results retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [HttpPost]
        [EndpointSummary("Create Poll")]
        [EndpointDescription("Creates a poll with a question and options for the signed-in user.")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(
            [FromBody] CreatePollRequest request,
            CancellationToken cancellationToken)
        {
            try
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
            catch (ValidationException ex)
            {
                return ApiExceptionResults.From(ex);
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [HttpPost("{id:guid}/publish")]
        [EndpointSummary("Publish Poll")]
        [EndpointDescription("Moves a draft poll owned by the signed-in user to active so it can receive votes.")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var poll = await _pollService.PublishAsync(
                    id,
                    User.GetRequiredUserId(),
                    cancellationToken);
                return Ok(ApiResponse.Ok(poll, "Poll published successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("poll-votes")]
        [HttpPost("{id:guid}/votes")]
        [EndpointSummary("Vote on a Poll")]
        [EndpointDescription("Casts one vote on the poll, either an option (pollOptionId) or a custom answer, with an optional reason. Accepts a signed-in user (Bearer JWT) or a guest (X-Guest-Token). One vote per voter; a repeat returns 409.")]
        [ProducesResponseType(typeof(ApiResponse<VoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public Task<IActionResult> Vote(
            Guid id,
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            request.PollId = id;
            return CastVoteAsync(request, cancellationToken);
        }

        [AllowAnonymous]
        [EnableRateLimiting("poll-votes")]
        [HttpPost("vote")]
        [EndpointSummary("Vote on a Poll (Id in Body)")]
        [EndpointDescription("Same as POST /{id}/votes but the poll id is sent in the request body. Accepts a user JWT or a guest token.")]
        [ProducesResponseType(typeof(ApiResponse<VoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public Task<IActionResult> VoteFromBody(
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            return CastVoteAsync(request, cancellationToken);
        }

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete Poll")]
        [EndpointDescription("Deletes a poll owned by the signed-in user.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                await _pollService.DeletePollAsync(id, User.GetRequiredUserId(), cancellationToken);
                return NoContent();
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        private async Task<IActionResult> CastVoteAsync(
            CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                // Voting accepts a Yakku user (Bearer JWT) or a guest (X-Guest-Token header / cookie).
                var voter = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                var result = await _voteService.CastAsync(
                    request.PollId,
                    voter.UserId ?? voter.GuestId!.Value,
                    voter.IsUser ? VoterRole.User : VoterRole.Guest,
                    request,
                    cancellationToken);
                return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
            }
            catch (ValidationException ex)
            {
                return ApiExceptionResults.From(ex);
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        private Guid? CurrentUserId()
        {
            return User.TryGetUserId(out var userId) ? userId : null;
        }
    }
}


