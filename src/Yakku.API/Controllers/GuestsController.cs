using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Yakku.API.Guests;
using Yakku.API.Media;
using Yakku.API.Middleware;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.DTOs;

namespace Yakku.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/guests")]
    public class GuestsController : ControllerBase
    {
        private readonly GuestCookieService _guestCookieService;
        private readonly IValidator<CreateGuestRequest> _validator;
        private readonly ILogger<GuestsController> _logger;

        public GuestsController(
            GuestCookieService guestCookieService,
            IValidator<CreateGuestRequest> validator,
            ILogger<GuestsController> logger)
        {
            _guestCookieService = guestCookieService;
            _validator = validator;
            _logger = logger;
        }

        [HttpPost]
        [EnableRateLimiting("guest-create")]
        [EndpointSummary("Create or Reuse a Guest Identity")]
        [EndpointDescription(
            "Returns the caller's existing valid guest (cookie or X-Guest-Token header) without issuing a new token. "
            + "Otherwise mints one using the required body idempotencyKey; retries with the same key return the same guest and the same guestToken "
            + "(the key is as sensitive as the token). Authenticated callers get 409. On creation only, client details are stored from request headers "
            + "(User-Agent, Accept-Language, optional X-Cookies-Enabled, edge country, client IP).")]
        [ProducesResponseType(typeof(ApiResponse<GuestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Create(
            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateGuestRequest? request,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();  //remove later
            // One identity per session: a caller already signed in has no use for a guest identity.
            if (User.Identity?.IsAuthenticated == true)
            {
                return ApiExceptionResults.From(new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This session is already authenticated and does not need a guest identity."));
            }

            var hasGuestToken = !string.IsNullOrWhiteSpace(MediaCallerResolver.ReadGuestToken(HttpContext));
            if (!hasGuestToken)
            {
                // Minting requires a validated idempotency key.
                request ??= new CreateGuestRequest();
                await _validator.ValidateAndThrowAsync(request, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(request?.IdempotencyKey))
            {
                await _validator.ValidateAndThrowAsync(request, cancellationToken);
            }

            var result = await _guestCookieService.EnsureAsync(
                HttpContext,
                request?.IdempotencyKey,
                cancellationToken);

            var response = new GuestResponse
            {
                GuestId = result.GuestId,
                ExpiresAt = result.ExpiresAt,
                GuestToken = result.RawTokenToSet
            };
            stopwatch.Stop();
            _logger.LogInformation(
               "guest timing | POST guests controller total took {ElapsedMs} ms",
               stopwatch.Elapsed.TotalMilliseconds);

            return Ok(ApiResponse.Ok(response, "Guest created successfully"));
        }
    }
}
