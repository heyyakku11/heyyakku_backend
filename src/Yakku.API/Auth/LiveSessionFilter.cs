using Microsoft.AspNetCore.Mvc.Filters;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Auth
{
    /// <summary>
    /// Access tokens outlive a logout by their lifetime. Endpoints that expose user content (for example the
    /// destination of a push notification) also require the token's login session to be live right now.
    /// </summary>
    public sealed class LiveSessionFilter : IAsyncActionFilter
    {
        private readonly IUserSessionRepository _sessions;

        public LiveSessionFilter(IUserSessionRepository sessions)
        {
            _sessions = sessions;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var user = context.HttpContext.User;
            var userId = user.GetRequiredUserId();
            var sessionId = user.GetRequiredSessionId();

            if (!await _sessions.IsLiveAsync(
                    sessionId, userId, DateTime.UtcNow, context.HttpContext.RequestAborted))
            {
                throw new AppException(
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "Invalid or expired session.");
            }

            await next();
        }
    }
}
