using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Application.Community.DTOs;
using Yakku.Application.Community.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/v1/community")]
    public class CommunityController : ControllerBase
    {
        private readonly ICommunityWaitlistService _waitlist;
        private readonly ICommunityQueuedQuestionService _queuedQuestions;

        public CommunityController(
            ICommunityWaitlistService waitlist,
            ICommunityQueuedQuestionService queuedQuestions)
        {
            _waitlist = waitlist;
            _queuedQuestions = queuedQuestions;
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPost("notify-me")]
        [EndpointSummary("Notify Me When Community Is Live")]
        [EndpointDescription("Adds the signed-in user to the queue of people to be notified when the community goes live. Safe to call again: a user already in the queue gets 200 with alreadyJoined true, a new one gets 201.")]
        [ProducesResponseType(typeof(ApiResponse<CommunityWaitlistResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<CommunityWaitlistResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> NotifyMe(CancellationToken cancellationToken)
        {
            var result = await _waitlist.JoinAsync(User.GetRequiredUserId(), cancellationToken);
            if (result.AlreadyJoined)
            {
                return Ok(ApiResponse.Ok(result, "You are already on the list"));
            }

            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "We will notify you when the community is live"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpGet("queued-questions")]
        [EndpointSummary("List Queued Community Questions")]
        [EndpointDescription("Returns the caller's queued community questions (oldest first), each with category tags and optional personality trait chips when the completed card is shared to community.")]
        [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CommunityQueuedQuestionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListQueuedQuestions(CancellationToken cancellationToken)
        {
            var result = await _queuedQuestions.ListAsync(User.GetRequiredUserId(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Queued questions retrieved"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPost("queued-questions")]
        [EndpointSummary("Queue a Community Question")]
        [EndpointDescription("Creates a queued community question for the caller. At most 3 per user. Requires one or more allow-listed category tags.")]
        [ProducesResponseType(typeof(ApiResponse<CommunityQueuedQuestionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateQueuedQuestion(
            [FromBody] UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _queuedQuestions.CreateAsync(User.GetRequiredUserId(), request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result, "Question queued"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpPut("queued-questions/{id:guid}")]
        [EndpointSummary("Update a Queued Community Question")]
        [EndpointDescription("Updates the caller's queued community question. Returns 404 when the id is missing or not owned.")]
        [ProducesResponseType(typeof(ApiResponse<CommunityQueuedQuestionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateQueuedQuestion(
            Guid id,
            [FromBody] UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _queuedQuestions.UpdateAsync(User.GetRequiredUserId(), id, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Question updated"));
        }

        [ServiceFilter(typeof(LiveSessionFilter))]
        [HttpDelete("queued-questions/{id:guid}")]
        [EndpointSummary("Delete a Queued Community Question")]
        [EndpointDescription("Removes the caller's queued community question. Returns 404 when the id is missing or not owned.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteQueuedQuestion(Guid id, CancellationToken cancellationToken)
        {
            await _queuedQuestions.DeleteAsync(User.GetRequiredUserId(), id, cancellationToken);
            return Ok(ApiResponse.Ok<object?>(null, "Question removed"));
        }
    }
}
