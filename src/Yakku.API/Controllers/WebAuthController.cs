using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Yakku.API.Guests;
using Yakku.API.Middleware;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Controllers
{
    /// <summary>
    /// Phone-OTP sign-in for the website only. The mobile app keeps using <c>/api/v1/auth</c>.
    /// The website gets a single 15-minute access token and no refresh token, and there is no refresh route: when the
    /// token ends the visitor signs in again from the start.
    /// </summary>
    [AllowAnonymous]
    [ApiController]
    [Route("api/v1/web/auth")]
    public class WebAuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IWebSessionService _webSessions;
        private readonly IGuestMediaClaimService _guestMediaClaim;
        private readonly GuestCookieService _guestCookies;
        private readonly ILogger<WebAuthController> _logger;

        public WebAuthController(
            IAuthService authService,
            IWebSessionService webSessions,
            IGuestMediaClaimService guestMediaClaim,
            GuestCookieService guestCookies,
            ILogger<WebAuthController> logger)
        {
            _authService = authService;
            _webSessions = webSessions;
            _guestMediaClaim = guestMediaClaim;
            _guestCookies = guestCookies;
            _logger = logger;
        }

        [EnableRateLimiting("otp-send")]
        [HttpPost("send")]
        [EndpointSummary("Send OTP for Website Sign-in")]
        [EndpointDescription("Website only. Same behaviour as POST /api/v1/auth/send: queues an OTP SMS for login or registration (Indian mobile numbers only). The OTP is never returned.")]
        [ProducesResponseType(typeof(ApiResponse<RequestOtpResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> RequestOtp(
            [FromBody] RequestOtpRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _authService.RequestOtpAsync(request, cancellationToken);
                return Ok(ApiResponse.Ok(result, "OTP sent successfully"));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(HttpContext, ex);
            }
        }

        [EnableRateLimiting("otp-verify")]
        [HttpPost("verify")]
        [EndpointSummary("Verify OTP and Sign In (Website)")]
        [EndpointDescription(
            "Website only. Verifies the OTP, registers new users, and moves the visitor's guest votes (polls, checks, "
            + "personality cards) to the account, using the X-Guest-Token header or yakku_guest cookie. The guest is then "
            + "deleted and the yakku_guest cookie cleared. Returns one access token valid for 15 minutes (900 seconds) and "
            + "no refresh token; there is no refresh route for the website.")]
        [ProducesResponseType(typeof(ApiResponse<WebVerifyOtpResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] WebVerifyOtpRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var verified = await _authService.VerifyOtpForWebAsync(
                    new VerifyOtpRequest { PhoneNumber = request.PhoneNumber, Otp = request.Otp },
                    cancellationToken);

                // Claim first, so the token is only handed out once the guest's votes are on the account.
                var claim = await GuestClaimHandler.ClaimAsync(
                    HttpContext,
                    _guestMediaClaim,
                    _guestCookies,
                    _logger,
                    verified.UserId,
                    nameof(WebAuthController),
                    cancellationToken);

                var accessToken = await _webSessions.StartAsync(verified.UserId, cancellationToken);

                var response = new WebVerifyOtpResponse
                {
                    AccessToken = accessToken,
                    ExpiresInSeconds = JwtOptions.WebSessionExpiresInSeconds,
                    IsNewUser = verified.IsNewUser,
                    GuestClaim = claim switch
                    {
                        GuestClaimResult.Claimed => "claimed",
                        GuestClaimResult.Failed => "failed",
                        _ => "none"
                    }
                };

                var message = verified.IsNewUser ? "Registration successful" : "Login successful";
                return Ok(ApiResponse.Ok(response, message));
            }
            catch (AppException ex)
            {
                return ApiExceptionResults.From(HttpContext, ex);
            }
        }
    }
}
