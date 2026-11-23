using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal sealed class FluentValidationExceptionHandler : IExceptionHandler
    {
        private readonly IAuditLogWriter _audit;

        public FluentValidationExceptionHandler(IAuditLogWriter audit)
        {
            _audit = audit;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not ValidationException validationException)
            {
                return false;
            }

            await ExceptionSystemLog.WriteAsync(
                _audit,
                validationException,
                nameof(FluentValidationExceptionHandler),
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                StatusCodes.Status400BadRequest,
                cancellationToken: CancellationToken.None);

            var response = ApiResponse.Fail(
                "Validation failed",
                ApiErrorMapper.FromValidationException(validationException));

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
