using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ISessionService _sessionService;

        public AuthController(IAuthService authService, ISessionService sessionService)
        {
            _authService = authService;
            _sessionService = sessionService;
        }

        [HttpPost("/api/v1/auth/send-otp")]
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

        [HttpPost("/api/v1/auth/verify-otp")]
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

        [HttpPost("/api/v1/auth/refresh")]
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

        [HttpPost("/api/v1/auth/logout")]
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
        [HttpPost("/api/v1/auth/logout-all")]
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
