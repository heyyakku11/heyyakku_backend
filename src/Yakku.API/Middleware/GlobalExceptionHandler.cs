using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.API.Middleware
{
    internal sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IAuditLogWriter _audit;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IAuditLogWriter audit)
        {
            _logger = logger;
            _audit = audit;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");

            // Only the exception type and the route are persisted; messages can carry user input or SQL values.
            await _audit.WriteSystemAsync(
                new SystemLogEntry(
                    SystemLogCategory.Exception,
                    SystemLogSeverity.Error,
                    nameof(GlobalExceptionHandler),
                    "Unhandled exception while processing a request.",
                    ExceptionType: exception.GetType().FullName ?? exception.GetType().Name,
                    HttpMethod: httpContext.Request.Method,
                    Path: httpContext.Request.Path.Value,
                    StatusCode: StatusCodes.Status500InternalServerError),
                CancellationToken.None);

            var response = ApiResponse.Fail(
                "An unexpected error occurred",
                [
                    new ApiError
                    {
                        Code = ApiErrorCodes.InternalServerError,
                        Message = "Something went wrong"
                    }
                ]);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
