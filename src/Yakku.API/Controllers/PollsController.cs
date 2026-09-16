using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.API.Guests;
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
        [HttpPost()]
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

        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<PollResponse>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _pollService.GetPollsAsync(cursor, cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<PollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                var poll = await _pollService.GetPollDetailsAsync(id, cancellationToken);
                return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [AllowAnonymous]
        [HttpGet("share/{shareToken}")]
        [ProducesResponseType(typeof(ApiResponse<SharedPollResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByShareToken(
            string shareToken,
            CancellationToken cancellationToken)
        {
            try
            {
                var poll = await _pollService.GetSharedPollByTokenAsync(shareToken, cancellationToken);
                return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [Authorize]
        [HttpPost("vote")]
        [ProducesResponseType(typeof(ApiResponse<VoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Vote(
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _voteService.CastAsync(
                    request.PollId,
                    User.GetRequiredUserId(),
                    null,
                    request,
                    cancellationToken);
                return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
            }
            catch (ValidationException ex)
            {
                return ToValidationErrorResult(ex);
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [AllowAnonymous]
        [HttpPost("guest-vote")]
        [ProducesResponseType(typeof(ApiResponse<VoteResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> GuestVote(
            [FromBody] CastVoteRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var guestId = (await _guestCookieService.EnsureAsync(HttpContext, cancellationToken)).GuestId;

                var result = await _voteService.CastAsync(
                    request.PollId,
                    null,
                    guestId,
                    request,
                    cancellationToken);
                return Ok(ApiResponse.Ok(result, "Vote submitted successfully"));
            }
            catch (ValidationException ex)
            {
                return ToValidationErrorResult(ex);
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        private static BadRequestObjectResult ToValidationErrorResult(ValidationException exception)
        {
            return new BadRequestObjectResult(
                ApiResponse.Fail(
                    "Validation failed",
                    ApiErrorMapper.FromValidationException(exception)));
        }

        private static ObjectResult ToErrorResult(AppException exception)
        {
            var response = ApiResponse.Fail(
                exception.Message,
                [
                    new ApiError
                    {
                        Code = exception.ErrorCode,
                        Message = exception.ErrorMessage,
                        Field = exception.Field
                    }
                ]);

            return new ObjectResult(response)
            {
                StatusCode = exception.StatusCode
            };
        }
    }
}
