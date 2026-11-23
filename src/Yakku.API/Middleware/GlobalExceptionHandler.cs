using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Common.Responses;

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

            await ExceptionSystemLog.WriteAsync(
                _audit,
                exception,
                nameof(GlobalExceptionHandler),
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                statusCode,
                extraMetadata: schemaMismatch
                    ? new Dictionary<string, string?> { ["schemaMismatch"] = "true" }
                    : null,
                cancellationToken: CancellationToken.None);

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
