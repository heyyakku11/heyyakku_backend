using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Auth;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Ask Back: a private conversation between a poll or check owner and one voter about that voter's reason.
    /// Both sides appear only by their anonymous names. Real-time events are on the hub at /hubs/ask-back.
    /// </summary>
    [Authorize]
    [ServiceFilter(typeof(LiveSessionFilter))]
    [ApiController]
    [Route("api/v1/ask-back/conversations")]
    public class AskBackController : ControllerBase
    {
        private readonly IAskBackService _askBack;

        public AskBackController(IAskBackService askBack)
        {
            _askBack = askBack;
        }

        [HttpPost]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Start an Ask Back conversation")]
        [EndpointDescription("Only the owner of the poll or check can start one, for a vote by a signed-in user that has a reason. Send just the voteId; the participants are worked out on the server. One conversation exists per vote: asking again returns the existing one with 200, a new one returns 201. Returns 404 when the vote is not the caller's to ask about, 400 for the caller's own vote or a vote without a reason, 403 for a guest voter or a disabled account.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Create(
            [FromBody] CreateAskBackConversationRequest request,
            CancellationToken cancellationToken)
        {
            var (conversation, created) = await _askBack.CreateConversationAsync(
                User.GetRequiredUserId(), request, cancellationToken);
            return StatusCode(
                created ? StatusCodes.Status201Created : StatusCodes.Status200OK,
                ApiResponse.Ok(conversation, created ? "Conversation started" : "Conversation retrieved"));
        }

        [HttpGet]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("List my Ask Back conversations")]
        [EndpointDescription("The caller's conversations, most recently active first, each with the other person's anonymous name, a last-message preview and the unread count. Cursor pagination: pass meta.nextCursor as cursor for the next page. limit defaults to 20 (max 50).")]
        [ProducesResponseType(typeof(ApiResponse<List<AskBackConversationListItemResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> List(
            [FromQuery] string? cursor,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var page = await _askBack.ListConversationsAsync(User.GetRequiredUserId(), cursor, limit, cancellationToken);
            return Ok(ApiResponse.Ok(
                page.Items,
                "Conversations retrieved successfully",
                PaginationMeta.ForCursor(page.Items.Count, page.NextCursor, page.HasMore)));
        }

        [HttpGet("{conversationId:guid}")]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("Get an Ask Back conversation")]
        [EndpointDescription("The conversation with the other person's anonymous name and the original question, picked option and reason. Returns 404 unless the caller is one of its two participants.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.GetConversationAsync(User.GetRequiredUserId(), conversationId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation retrieved successfully"));
        }

        [HttpGet("{conversationId:guid}/messages")]
        [EnableRateLimiting("ask-back-read")]
        [EndpointSummary("List messages")]
        [EndpointDescription("Messages, oldest first within the page. Without a cursor: the newest page. direction=older (default) with a cursor: the page before it, for scrolling up. direction=newer with a cursor (a message's cursor): the messages after it, for catching up after a reconnect. Use meta.nextCursor to continue while meta.hasMore. limit defaults to 30 (max 50). isMine says whether the caller sent it; the sender's user id is never returned.")]
        [ProducesResponseType(typeof(ApiResponse<List<AskBackMessageResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ListMessages(
            Guid conversationId,
            [FromQuery] string? cursor,
            [FromQuery] string? direction,
            [FromQuery] int? limit,
            CancellationToken cancellationToken)
        {
            var page = await _askBack.ListMessagesAsync(
                User.GetRequiredUserId(), conversationId, cursor, direction, limit, cancellationToken);
            return Ok(ApiResponse.Ok(
                page.Items,
                "Messages retrieved successfully",
                PaginationMeta.ForCursor(page.Items.Count, page.NextCursor, page.HasMore)));
        }

        [HttpPost("{conversationId:guid}/messages")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Send a message")]
        [EndpointDescription("Content is 1 to 2000 characters. clientMessageId makes a retry safe: sending the same one again returns the stored message instead of a second one. Returns 409 when the conversation is closed or blocked, 404 unless the caller is a participant.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackMessageResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Send(
            Guid conversationId,
            [FromBody] SendAskBackMessageRequest request,
            CancellationToken cancellationToken)
        {
            var message = await _askBack.SendMessageAsync(
                User.GetRequiredUserId(), conversationId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(message, "Message sent"));
        }

        [HttpPatch("{conversationId:guid}/read")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Mark messages as read")]
        [EndpointDescription("Moves the caller's read position to lastReadMessageId. A position that is not newer than the current one is ignored (advanced=false). Returns the caller's unread count. 404 for a message of another conversation.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackReadResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> MarkRead(
            Guid conversationId,
            [FromBody] MarkAskBackReadRequest request,
            CancellationToken cancellationToken)
        {
            var result = await _askBack.MarkReadAsync(User.GetRequiredUserId(), conversationId, request, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Read position updated"));
        }

        [HttpPut("{conversationId:guid}/mute")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Mute a conversation")]
        [EndpointDescription("Stops push notifications for this conversation for the caller. Messages still arrive. Idempotent.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Mute(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.SetMutedAsync(User.GetRequiredUserId(), conversationId, true, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation muted"));
        }

        [HttpDelete("{conversationId:guid}/mute")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Unmute a conversation")]
        [EndpointDescription("Turns push notifications for this conversation back on for the caller. Idempotent.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Unmute(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.SetMutedAsync(User.GetRequiredUserId(), conversationId, false, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation unmuted"));
        }

        [HttpPost("{conversationId:guid}/close")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Close a conversation")]
        [EndpointDescription("Either participant can close an active conversation. It stays readable; nobody can send. Only the asker can reopen it. Idempotent. 409 when it is blocked.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Close(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.CloseAsync(User.GetRequiredUserId(), conversationId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation closed"));
        }

        [HttpPost("{conversationId:guid}/reopen")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Reopen a conversation")]
        [EndpointDescription("Only the asker (the poll or check owner) can reopen a closed conversation. 403 for the voter, 409 when it is blocked. Idempotent when already active.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reopen(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.ReopenAsync(User.GetRequiredUserId(), conversationId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation reopened"));
        }

        [HttpPost("{conversationId:guid}/block")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Block the other participant")]
        [EndpointDescription("Blocks the conversation for both sides: read-only until the person who blocked lifts it. An optional reason (up to 500 characters) is also recorded as a report for moderation. This does not block the person anywhere else in Yakku. Idempotent.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> Block(
            Guid conversationId,
            [FromBody] BlockAskBackRequest? request,
            CancellationToken cancellationToken)
        {
            var result = await _askBack.BlockAsync(
                User.GetRequiredUserId(), conversationId, request ?? new BlockAskBackRequest(), cancellationToken);
            return Ok(ApiResponse.Ok(result, "Conversation blocked"));
        }

        [HttpPost("{conversationId:guid}/unblock")]
        [EnableRateLimiting("ask-back-write")]
        [EndpointSummary("Lift a block")]
        [EndpointDescription("Only the person who blocked can lift it; the conversation becomes active again. 403 for the other participant. Idempotent when not blocked.")]
        [ProducesResponseType(typeof(ApiResponse<AskBackConversationResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Unblock(Guid conversationId, CancellationToken cancellationToken)
        {
            var result = await _askBack.UnblockAsync(User.GetRequiredUserId(), conversationId, cancellationToken);
            return Ok(ApiResponse.Ok(result, "Block lifted"));
        }
    }
}
