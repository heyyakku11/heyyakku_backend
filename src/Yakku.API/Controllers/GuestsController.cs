using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Guests;
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

        public GuestsController(GuestCookieService guestCookieService)
        {
            _guestCookieService = guestCookieService;
        }

        [HttpPost]
        [EnableRateLimiting("guest-create")]
        [EndpointSummary("Create or Reuse a Guest Identity")]
        [EndpointDescription("Returns the caller's existing valid guest (cookie or X-Guest-Token header) without issuing a new token; otherwise creates one. Authenticated callers get 409.")]
        [ProducesResponseType(typeof(ApiResponse<GuestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            // One identity per session: a caller already signed in has no use for a guest identity.
            if (User.Identity?.IsAuthenticated == true)
            {
                return ApiExceptionResults.From(new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This session is already authenticated and does not need a guest identity."));
            }

            var result = await _guestCookieService.EnsureAsync(HttpContext, cancellationToken);

            var response = new GuestResponse
            {
                GuestId = result.GuestId,
                ExpiresAt = result.ExpiresAt,
                GuestToken = result.RawTokenToSet
            };

            return Ok(ApiResponse.Ok(response, "Guest created successfully"));
        }
    }
}
