using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.API.Middleware;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Interfaces;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Everything a signed-in user does with their polls: create, list (asked, answered, saved), open, close,
    /// delete and save. Voting and public share links live under <c>/api/v1/votes</c> and <c>/api/v1/shares</c>.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/v1/polls")]
    public class PollsController : ControllerBase
    {
        private readonly IPollService _pollService;
        private readonly IUserService _userService;
        private readonly ISavedPollService _savedPollService;
        private readonly IReasonService _reasonService;

        public PollsController(
            IPollService pollService,
            IUserService userService,
            ISavedPollService savedPollService,
            IReasonService reasonService)
        {
            _reasonService = reasonService;
            _pollService = pollService;
            _userService = userService;
            _savedPollService = savedPollService;
        }

        [HttpPost]
        [EndpointSummary("Create Poll")]
        [EndpointDescription("Creates an active poll owned by the signed-in user. Send a question, 2 to 3 options and a content type (question, image, video or audio). Image, video and audio polls require a completed media upload (mediaUploadSessionId). The response includes the poll's shareCode for the universal share link. Returns 201.")]
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
                return StatusCode(
                    StatusCodes.Status201Created,
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

        [HttpGet("asked")]
        [EndpointSummary("List My Asks")]
        [EndpointDescription("Lists everything the signed-in user made, polls and checks together, newest first. Each item has a kind (poll or check); for a check, pollId is the made-check id and check carries its title, colour and sticker. Uses cursor pagination: pass the cursor from the previous response's meta to get the next page.")]
        [ProducesResponseType(typeof(ApiResponse<List<UserPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAsked(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetAskedPollsAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [HttpGet("answered")]
        [EndpointSummary("List Polls I Answered")]
        [EndpointDescription("Lists the polls the signed-in user has voted on, most recently voted first. Uses cursor pagination: pass the cursor from the previous response's meta to get the next page.")]
        [ProducesResponseType(typeof(ApiResponse<List<UserPollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAnswered(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _userService.GetAnsweredPollsAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Polls retrieved successfully", result.Meta));
        }

        [HttpGet("saved")]
        [EndpointSummary("List Saved Polls")]
        [EndpointDescription("Lists the polls the signed-in user has saved, most recently saved first. Uses cursor pagination: pass the cursor from the previous response's meta to get the next page. Returns 400 for an invalid cursor.")]
        [ProducesResponseType(typeof(ApiResponse<List<PollResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetSaved(
            [FromQuery] string? cursor,
            CancellationToken cancellationToken)
        {
            var result = await _savedPollService.ListAsync(
                User.GetRequiredUserId(),
                cursor,
                cancellationToken);

            return Ok(ApiResponse.Ok(result.Items, "Saved polls retrieved successfully", result.Meta));
        }

        [HttpGet("{id:guid}")]
        [EndpointSummary("Get My Poll")]
        [EndpointDescription("Returns the details and results of a poll the signed-in user owns, including how their own answer compares with the crowd. Returns 404 when the poll does not exist or belongs to someone else. Other people open a poll through its share link: GET /api/v1/shares/{shareCode}.")]
        [ProducesResponseType(typeof(ApiResponse<UserPollDetailResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        {
            var poll = await _userService.GetOwnedPollDetailsAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(poll, "Poll retrieved successfully"));
        }

        [HttpPost("{id:guid}/close")]
        [EndpointSummary("Close Poll")]
        [EndpointDescription("Closes a poll the signed-in user owns so it stops accepting votes. Existing votes are kept and results stay available. Returns 404 when the poll does not exist or is not yours, and 409 when it cannot be closed (for example it is not active).")]
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

        [HttpDelete("{id:guid}")]
        [EndpointSummary("Delete Poll")]
        [EndpointDescription("Permanently deletes a poll the signed-in user owns, together with its options, votes and uploaded media. Returns 404 when the poll does not exist or is not yours, and 409 when its status does not allow deletion.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        {
            await _pollService.DeletePollAsync(
                id,
                User.GetRequiredUserId(),
                cancellationToken);

            return Ok(ApiResponse.Ok<object?>(null, "Poll deleted successfully"));
        }

        [HttpGet("{id:guid}/options/{optionId:guid}/reasons")]
        [EndpointSummary("List Reasons For An Option")]
        [EndpointDescription("Lists the reasons voters gave for one option of a poll the signed-in user owns, newest first. Anonymous: no author is returned. Empty until the results are unlocked. Returns 404 when the poll is not yours or the option is not on it.")]
        [ProducesResponseType(typeof(ApiResponse<List<ReasonResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOptionReasons(Guid id, Guid optionId, CancellationToken cancellationToken)
        {
            var reasons = await _reasonService.GetPollOptionReasonsAsync(
                id,
                User.GetRequiredUserId(),
                optionId,
                cancellationToken);

            return Ok(ApiResponse.Ok(reasons, "Reasons retrieved successfully"));
        }

        [HttpGet("{id:guid}/custom-answers")]
        [EndpointSummary("List Custom Answers")]
        [EndpointDescription("Lists the custom answers (\"their own options\") voters wrote in on a poll the signed-in user owns, newest first, each with the reason the voter gave, if any. Anonymous: no author is returned. The asker's own answer is never included. Empty until the results are unlocked. Returns 404 when the poll is not yours.")]
        [ProducesResponseType(typeof(ApiResponse<List<CustomAnswerResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetCustomAnswers(Guid id, CancellationToken cancellationToken)
        {
            var answers = await _reasonService.GetPollCustomAnswersAsync(id, User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(answers, "Custom answers retrieved successfully"));
        }

        [HttpPost("{id:guid}/reasons/{reasonId:guid}/report")]
        [EndpointSummary("Report A Reason")]
        [EndpointDescription("Reports a reason on a poll the signed-in user owns as abuse. The reason is removed from the vote so it is no longer shown or counted, and a report is logged for moderation. Returns the reason counts as they are now (optionWhyCount, totalWhyCount). Returns 404 when the poll is not yours or the reason does not exist (or was already removed).")]
        [ProducesResponseType(typeof(ApiResponse<ReasonReportResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ReportReason(Guid id, Guid reasonId, CancellationToken cancellationToken)
        {
            var result = await _reasonService.ReportPollReasonAsync(id, User.GetRequiredUserId(), reasonId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Reason reported"));
        }

        [HttpPut("{id:guid}/save")]
        [EndpointSummary("Save Poll")]
        [EndpointDescription("Adds a poll to the signed-in user's saved polls. Idempotent: saving an already saved poll succeeds and changes nothing. Returns 404 when the poll does not exist.")]
        [ProducesResponseType(typeof(ApiResponse<SavedPollStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Save(Guid id, CancellationToken cancellationToken)
        {
            var result = await _savedPollService.SaveAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Poll saved successfully"));
        }

        [HttpDelete("{id:guid}/save")]
        [EndpointSummary("Unsave Poll")]
        [EndpointDescription("Removes a poll from the signed-in user's saved polls. Idempotent: removing a poll that is not saved succeeds and changes nothing.")]
        [ProducesResponseType(typeof(ApiResponse<SavedPollStateResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Unsave(Guid id, CancellationToken cancellationToken)
        {
            var result = await _savedPollService.UnsaveAsync(
                User.GetRequiredUserId(),
                id,
                cancellationToken);

            return Ok(ApiResponse.Ok(result, "Poll unsaved successfully"));
        }
    }
}
