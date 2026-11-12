using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Yakku.API.AdminSecurity;
using Yakku.API.Auth;
using Yakku.API.Middleware;
using Yakku.Application.AdminAuth.DTOs;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/auth")]
    public class AdminAuthController : ControllerBase
    {
        private readonly IAdminAuthService _adminAuthService;
        private readonly IAdminSessionService _adminSessionService;
        private readonly AdminCookieService _cookies;
        private readonly AdminCsrfTokens _csrf;

        public AdminAuthController(
            IAdminAuthService adminAuthService,
            IAdminSessionService adminSessionService,
            AdminCookieService cookies,
            AdminCsrfTokens csrf)
        {
            _adminAuthService = adminAuthService;
            _adminSessionService = adminSessionService;
            _cookies = cookies;
            _csrf = csrf;
        }

        [EnableRateLimiting("admin-auth")]
        [HttpPost("login")]
        [EndpointSummary("Admin Login")]
        [EndpointDescription("Logs an admin in with email and password. The session is set as an HttpOnly cookie and is never returned in the body; the body carries only the CSRF token to send back in the X-CSRF-Token header on state-changing requests. Returns 429 after repeated failures.")]
        [ProducesResponseType(typeof(ApiResponse<AdminAuthResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> Login(
            [FromBody] AdminLoginRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var session = await _adminAuthService.LoginAsync(
                    request,
                    new AdminClientInfo(
                        HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Request.Headers.UserAgent.ToString()),
                    cancellationToken);

                _cookies.Write(Response, session.Token, session.AbsoluteExpiresAt);
                return Ok(ApiResponse.Ok(
                    new AdminAuthResponse { CsrfToken = _csrf.Create(session.SessionId) },
                    "Login successful"));
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

        [EnableRateLimiting("admin-auth")]
        [HttpPost("register")]
        [EndpointSummary("Admin Registration (Step 1)")]
        [EndpointDescription("Registration step 1. Send email, password and the admin secret code. Any valid email is accepted. If the code matches, the email and password are kept in a temporary record for 15 minutes and a verification link (valid 10 minutes) is emailed. A wrong code creates nothing and is limited to 5 attempts per email.")]
        [ProducesResponseType(typeof(ApiResponse<AdminRegistrationStepResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public Task<IActionResult> Register(
            [FromBody] AdminRegisterRequest request,
            CancellationToken cancellationToken)
        {
            return RunAsync(
                async () => Ok(ApiResponse.Ok(
                    await _adminAuthService.RegisterAsync(request, cancellationToken),
                    "Verification email sent. Open the link to finish registering.")));
        }

        [EnableRateLimiting("admin-auth")]
        [HttpPost("verify-email")]
        [EndpointSummary("Verify Admin Email (Step 2)")]
        [EndpointDescription("Registration step 2. The token from the emailed link creates the admin account. The admin then logs in with the same email and password.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        public Task<IActionResult> VerifyEmail(
            [FromBody] AdminVerifyEmailRequest request,
            CancellationToken cancellationToken)
        {
            return RunAsync(async () =>
            {
                await _adminAuthService.VerifyEmailAsync(request, cancellationToken);
                return Ok(ApiResponse.Ok<object?>(null, "Registration complete. You can now log in."));
            });
        }

        [EnableRateLimiting("admin-auth")]
        [HttpPost("forgot-password")]
        [EndpointSummary("Request Admin Password Reset")]
        [EndpointDescription("Sends a password reset link (valid 10 minutes) when an admin with this email exists. The response is always the same so it cannot be used to discover admin emails.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> ForgotPassword(
            [FromBody] AdminForgotPasswordRequest request,
            CancellationToken cancellationToken)
        {
            return RunAsync(async () =>
            {
                await _adminAuthService.ForgotPasswordAsync(request, cancellationToken);
                return Ok(ApiResponse.Ok<object?>(null, "If an admin account exists for this email, a reset link has been sent."));
            });
        }

        [EnableRateLimiting("admin-auth")]
        [HttpPost("reset-password")]
        [EndpointSummary("Reset Admin Password")]
        [EndpointDescription("Sets a new password using the token from the reset email. The token works once, and all sessions are signed out.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        public Task<IActionResult> ResetPassword(
            [FromBody] AdminResetPasswordRequest request,
            CancellationToken cancellationToken)
        {
            return RunAsync(async () =>
            {
                await _adminAuthService.ResetPasswordAsync(request, cancellationToken);
                return Ok(ApiResponse.Ok<object?>(null, "Password reset successfully. Please log in."));
            });
        }

        [Authorize(Policy = "Admin")]
        [EnableRateLimiting("admin-auth")]
        [HttpPost("change-password")]
        [EndpointSummary("Change Admin Password")]
        [EndpointDescription("Changes the signed-in admin's password. Requires the current password, a new password and its confirmation. All sessions are signed out, so the admin logs in again.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public Task<IActionResult> ChangePassword(
            [FromBody] AdminChangePasswordRequest request,
            CancellationToken cancellationToken)
        {
            return RunAsync(async () =>
            {
                await _adminAuthService.ChangePasswordAsync(User.GetRequiredUserId(), request, cancellationToken);

                // Every session, this one included, was revoked on the server; drop the dead cookie too.
                _cookies.Clear(Response);
                return Ok(ApiResponse.Ok<object?>(null, "Password changed successfully. Please log in again."));
            });
        }

        [Authorize(Policy = "Admin")]
        [HttpGet("session")]
        [EndpointSummary("Get Admin Session")]
        [EndpointDescription("Returns the signed-in admin (email, role) and the CSRF token for this session. The frontend calls this on load to restore its state: the server cookie, not browser storage, is the source of truth. 401 when there is no valid session.")]
        [ProducesResponseType(typeof(ApiResponse<AdminSessionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public IActionResult GetSession()
        {
            return Ok(ApiResponse.Ok(
                new AdminSessionResponse
                {
                    Email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? string.Empty,
                    Role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
                    CsrfToken = _csrf.Create(User.GetRequiredSessionId())
                },
                "Session retrieved successfully"));
        }

        [Authorize(Policy = "Admin")]
        [HttpPost("logout")]
        [EndpointSummary("Admin Logout")]
        [EndpointDescription("Revokes this session on the server and clears the cookie. A stolen copy of the cookie stops working immediately.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(CancellationToken cancellationToken)
        {
            await _adminSessionService.RevokeAsync(
                User.GetRequiredUserId(),
                User.GetRequiredSessionId(),
                AdminSessionRevocationReasons.Logout,
                cancellationToken);
            _cookies.Clear(Response);
            return Ok(ApiResponse.Ok<object?>(null, "Logged out successfully"));
        }

        [Authorize(Policy = "Admin")]
        [HttpPost("logout-all")]
        [EndpointSummary("Admin Logout From All Sessions")]
        [EndpointDescription("Revokes every active session of the signed-in admin, on every device, and clears the cookie.")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
        {
            await _adminSessionService.RevokeAllAsync(
                User.GetRequiredUserId(),
                AdminSessionRevocationReasons.LogoutAll,
                cancellationToken);
            _cookies.Clear(Response);
            return Ok(ApiResponse.Ok<object?>(null, "Logged out from all sessions successfully"));
        }

        private static async Task<IActionResult> RunAsync(Func<Task<IActionResult>> action)
        {
            try
            {
                return await action();
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
