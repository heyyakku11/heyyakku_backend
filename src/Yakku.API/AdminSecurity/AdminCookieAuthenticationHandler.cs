using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Auth;
using Yakku.Application.Common.Responses;

namespace Yakku.API.AdminSecurity
{
    /// <summary>
    /// Authenticates an admin request from the HttpOnly session cookie. Every request is checked against the
    /// server-side session (exists, not revoked, not expired, admin still active), so logging out, suspending an
    /// admin or changing a password takes effect immediately. Roles come from the database, never from the client.
    /// </summary>
    public sealed class AdminCookieAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly AdminCookieService _cookies;
        private readonly IAdminSessionService _sessions;

        public AdminCookieAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            AdminCookieService cookies,
            IAdminSessionService sessions)
            : base(options, logger, encoder)
        {
            _cookies = cookies;
            _sessions = sessions;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var token = _cookies.Read(Request);
            if (string.IsNullOrEmpty(token))
            {
                return AuthenticateResult.NoResult();
            }

            var result = await _sessions.AuthenticateAsync(token, AdminClientInfoFactory.From(Context), Context.RequestAborted);
            if (!result.Succeeded)
            {
                // The browser keeps sending a dead cookie otherwise.
                _cookies.Clear(Response);
                return AuthenticateResult.Fail(result.FailureReason ?? "invalid_session");
            }

            if (result.RotatedToken is not null)
            {
                _cookies.Write(Response, result.RotatedToken, result.AbsoluteExpiresAt);
            }

            var identity = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, result.AdminId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, result.AdminId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, result.Email),
                new Claim(ClaimTypes.Role, result.Role.ToString()),
                new Claim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor),
                new Claim(AuthClaimTypes.SessionId, result.SessionId.ToString())
            ], Scheme.Name);

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            await Response.WriteAsJsonAsync(
                ApiResponse.Fail(
                    "Unauthorized.",
                    [new ApiError { Code = ApiErrorCodes.Unauthorized, Message = "Unauthorized." }]));
        }

        protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            await Response.WriteAsJsonAsync(
                ApiResponse.Fail(
                    "Forbidden.",
                    [new ApiError { Code = ApiErrorCodes.Forbidden, Message = "Forbidden." }]));
        }
    }

    internal static class AdminClientInfoFactory
    {
        public static AdminClientInfo From(HttpContext context) =>
            new(context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString());
    }
}
