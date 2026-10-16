using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.API.Guests;
using Yakku.API.Media;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISessionService _sessionService;
        private readonly IGuestMediaClaimService _guestMediaClaim;
        private readonly GuestCookieService _guestCookies;

        public AuthController(
            IAuthService authService,
            ISessionService sessionService,
            IGuestMediaClaimService guestMediaClaim,
            GuestCookieService guestCookies)
        {
            _authService = authService;
            _sessionService = sessionService;
            _guestMediaClaim = guestMediaClaim;
            _guestCookies = guestCookies;
        }

        [HttpPost("send")]
        [EndpointSummary("Send OTP for User Authentication")]
        [EndpointDescription("Sends OTP for login or registration. New numbers must include a name.")]
        [ProducesResponseType(typeof(ApiResponse<RequestOtpResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
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
                return ToErrorResult(ex);
            }
        }

        [HttpPost("verify")]
        [EndpointSummary("Verify OTP and Authenticate User")]
        [EndpointDescription("Verifies OTP, registers new users, and returns access/refresh tokens.")]
        [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> VerifyOtp(
            [FromBody] VerifyOtpRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _authService.VerifyOtpAsync(request, cancellationToken);

                var guestToken = MediaCallerResolver.ReadGuestToken(HttpContext);
                if (!string.IsNullOrWhiteSpace(guestToken))
                {
                    await _guestMediaClaim.ClaimAndDeleteGuestAsync(
                        guestToken,
                        result.UserId,
                        cancellationToken);
                    _guestCookies.Clear(HttpContext);
                }

                var message = result.Purpose == "Registration"
                    ? "Registration successful"
                    : "Login successful";

                return Ok(ApiResponse.Ok(result, message));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("refresh")]
        [EndpointSummary("Refresh Access Token Using Refresh Token")]
        [EndpointDescription("Exchanges a valid refresh token for a new access/refresh pair.")]
        [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Refresh(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _sessionService.RefreshAsync(request.RefreshToken, cancellationToken);
                return Ok(ApiResponse.Ok(result, "Token refreshed successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [HttpPost("logout")]
        [EndpointSummary("Logout Current Session and Revoke Refresh Token")]
        [EndpointDescription("Revokes the given refresh token and ends that session.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                await _sessionService.RevokeAsync(request.RefreshToken, cancellationToken);
                return Ok(ApiResponse.Ok<object?>(null, "Logged out successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
        }

        [Authorize]
        [HttpPost("logout-all")]
        [EndpointSummary("Revoke All Active Sessions for the Authenticated User")]
        [EndpointDescription("Requires auth. Revokes every active session for the current user.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
        {
            try
            {
                await _sessionService.RevokeAllAsync(User.GetRequiredUserId(), cancellationToken);
                return Ok(ApiResponse.Ok<object?>(null, "Logged out from all sessions successfully"));
            }
            catch (AppException ex)
            {
                return ToErrorResult(ex);
            }
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
