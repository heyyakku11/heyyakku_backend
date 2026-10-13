using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");

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
