using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.API.Middleware;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/polls")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _pollService;
        private readonly IVoteService _voteService;

        public PollsController(IPollService pollService, IVoteService voteService)
        {
            _pollService = pollService;
            _voteService = voteService;
        }

        [AllowAnonymous]
        [HttpGet]
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

        [EnableRateLimiting("poll-votes")]
        [HttpPost("{id:guid}/votes")]
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

        [EnableRateLimiting("poll-votes")]
        [HttpPost("vote")]
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
                var result = await _voteService.CastAsync(
                    request.PollId,
                    User.GetRequiredUserId(),
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


