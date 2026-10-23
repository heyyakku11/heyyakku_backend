using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Media;
using Yakku.API.Middleware;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/v1/media-upload-sessions")]
    public class MediaUploadSessionsController : ControllerBase
    {
        private readonly IMediaUploadSessionService _sessions;
        private readonly MediaCallerResolver _callerResolver;

        public MediaUploadSessionsController(
            IMediaUploadSessionService sessions,
            MediaCallerResolver callerResolver)
        {
            _sessions = sessions;
            _callerResolver = callerResolver;
        }

        [EnableRateLimiting("media-upload")]
        [HttpPost]
        [EndpointSummary("Create Media Upload Session")]
        [EndpointDescription("Starts an upload session for a poll image, audio or video and returns where to upload. Works for a signed-in user or a guest.")]
        [ProducesResponseType(typeof(ApiResponse<MediaUploadSessionResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status415UnsupportedMediaType)]
        public async Task<IActionResult> Create(
            [FromBody] CreateMediaUploadSessionRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var owner = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                var result = await _sessions.CreateAsync(request, owner, cancellationToken);
                return CreatedAtAction(
                    nameof(GetById),
                    new { sessionId = result.SessionId },
                    ApiResponse.Ok(result, "Upload session created successfully"));
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

        [HttpGet("{sessionId:guid}")]
        [EndpointSummary("Get Media Upload Session")]
        [EndpointDescription("Returns the status of one of the caller's upload sessions.")]
        [ProducesResponseType(typeof(ApiResponse<MediaUploadSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var owner = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                var result = await _sessions.GetAsync(sessionId, owner, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Upload session retrieved successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [HttpGet("{sessionId:guid}/access")]
        [EndpointSummary("Get Media Upload Access")]
        [EndpointDescription("Returns fresh time-limited upload/download URLs for one of the caller's upload sessions.")]
        [ProducesResponseType(typeof(ApiResponse<MediaAccessResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> GetAccess(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var owner = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                var result = await _sessions.GetAccessAsync(sessionId, owner, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Media access granted"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }

        [HttpPost("{sessionId:guid}/complete")]
        [EndpointSummary("Complete Media Upload")]
        [EndpointDescription("Marks an upload session as uploaded so the media can be attached to a poll.")]
        [ProducesResponseType(typeof(ApiResponse<MediaUploadSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Complete(
            Guid sessionId,
            [FromBody] CompleteMediaUploadRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var owner = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                var result = await _sessions.CompleteAsync(sessionId, owner, request, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Upload completed successfully"));
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

        [HttpPost("{sessionId:guid}/cancel")]
        [EndpointSummary("Cancel Media Upload Session")]
        [EndpointDescription("Cancels one of the caller's pending upload sessions and discards its media.")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken)
        {
            try
            {
                var owner = await _callerResolver.ResolveRequiredAsync(HttpContext, cancellationToken);
                await _sessions.CancelAsync(sessionId, owner, cancellationToken);
                return NoContent();
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(ex);
            }
        }
    }
}
