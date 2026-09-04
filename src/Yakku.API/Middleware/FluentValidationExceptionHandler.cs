using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal sealed class FluentValidationExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not ValidationException validationException)
            {
                return false;
            }

            var response = ApiResponse.Fail(
                "Validation failed",
                ApiErrorMapper.FromValidationException(validationException));

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
