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

            // The database is behind the deployed code (for example a migration has not been applied yet).
            // Say so, and ask the caller to retry, instead of a generic error.
            var schemaMismatch = DatabaseSchemaErrors.IsSchemaMismatch(exception);
            var statusCode = schemaMismatch ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status500InternalServerError;

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
                    StatusCode: statusCode),
                CancellationToken.None);

            var response = schemaMismatch
                ? ApiResponse.Fail(
                    "The service is being updated",
                    [
                        new ApiError
                        {
                            Code = ApiErrorCodes.ServiceUnavailable,
                            Message = "Yakku is being updated right now. Please try again in a few minutes."
                        }
                    ])
                : ApiResponse.Fail(
                    "An unexpected error occurred",
                    [
                        new ApiError
                        {
                            Code = ApiErrorCodes.InternalServerError,
                            Message = "Something went wrong"
                        }
                    ]);

            if (schemaMismatch)
            {
                httpContext.Response.Headers.RetryAfter = "60";
            }

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
