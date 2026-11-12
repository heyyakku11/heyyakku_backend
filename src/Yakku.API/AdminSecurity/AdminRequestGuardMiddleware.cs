using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.API.AdminSecurity
{
    /// <summary>
    /// CSRF defence for the admin cookie. Browsers attach the cookie to any request to this host, so every
    /// state-changing admin request must (1) come from a trusted Origin (Referer as fallback) and (2), when it
    /// carries a session, send the session-bound token in X-CSRF-Token. SameSite is a second layer, not the defence.
    /// Safe methods are never state-changing here, so they pass untouched. Runs after authorization, so requests
    /// that already failed authentication never reach it.
    /// </summary>
    public sealed class AdminRequestGuardMiddleware
    {
        private const string AdminAuthPrefix = "/api/v1/admin/auth";

        private readonly RequestDelegate _next;

        public AdminRequestGuardMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            AdminSecuritySettings settings,
            AdminCsrfTokens csrf,
            IAuditLogWriter audit)
        {
            if (!IsStateChanging(context.Request.Method) || !IsAdminScope(context))
            {
                await _next(context);
                return;
            }

            // Login CSRF and cross-site posts to every admin route: the Origin must be one of ours.
            if (!IsTrustedOrigin(context.Request, settings))
            {
                await RejectAsync(context, audit, AuditEvents.Auth.OriginRejected, "untrusted_origin");
                return;
            }

            // The session-bound token is only required once there is a session to bind it to.
            if (settings.CsrfEnabled && context.User.Identity?.AuthenticationType == AdminSecuritySettings.AuthenticationScheme)
            {
                var sessionIdValue = context.User.FindFirstValue(AuthClaimTypes.SessionId);
                var header = context.Request.Headers[AdminSecuritySettings.CsrfHeaderName].ToString();
                if (!Guid.TryParse(sessionIdValue, out var sessionId) || !csrf.IsValid(sessionId, header))
                {
                    await RejectAsync(
                        context,
                        audit,
                        AuditEvents.Auth.CsrfRejected,
                        string.IsNullOrEmpty(header) ? "csrf_missing" : "csrf_invalid");
                    return;
                }
            }

            await _next(context);
        }

        private static bool IsStateChanging(string method) =>
            !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method));

        private static bool IsAdminScope(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments(AdminAuthPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return context.GetEndpoint()?.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Any(data => string.Equals(data.Policy, "Admin", StringComparison.Ordinal)) == true;
        }

        private static bool IsTrustedOrigin(HttpRequest request, AdminSecuritySettings settings)
        {
            var origin = request.Headers.Origin.ToString();
            if (!string.IsNullOrEmpty(origin))
            {
                return settings.TrustedOrigins.Contains(origin.TrimEnd('/'));
            }

            // Some browsers omit Origin on same-origin requests; Referer then tells us where the page came from.
            var referer = request.Headers.Referer.ToString();
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                return settings.TrustedOrigins.Contains($"{uri.Scheme}://{uri.Authority}");
            }

            // No Origin and no Referer on a state-changing request: not a browser we can vouch for.
            return false;
        }

        private static async Task RejectAsync(HttpContext context, IAuditLogWriter audit, string eventType, string reason)
        {
            var adminId = Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
            await audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    eventType,
                    AuditOutcome.Suspicious,
                    FailureReason: reason,
                    Metadata: new Dictionary<string, string?> { ["path"] = context.Request.Path.Value }),
                CancellationToken.None);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                ApiResponse.Fail(
                    "Forbidden.",
                    [new ApiError { Code = ApiErrorCodes.Forbidden, Message = "Request rejected." }]));
        }
    }
}
