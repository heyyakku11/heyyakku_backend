using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.API.Middleware
{
    internal sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly ISystemLogWriter _systemLogWriter;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            ISystemLogWriter systemLogWriter)
        {
            _logger = logger;
            _systemLogWriter = systemLogWriter;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "An unhandled exception occurred.");

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Error,
                    EventType = SystemLogEventTypes.UnhandledException,
                    Message = "An unhandled exception occurred.",
                    Path = Truncate(httpContext.Request.Path.Value, 256),
                    Details = new
                    {
                        exceptionType = exception.GetType().FullName,
                        exceptionMessage = Truncate(exception.Message, 500)
                    }
                },
                cancellationToken);

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

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}
