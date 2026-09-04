using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal sealed class AppExceptionHandler : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not AppException appException)
            {
                return false;
            }

            var response = ApiResponse.Fail(
                appException.Message,
                [
                    new ApiError
                    {
                        Code = appException.ErrorCode,
                        Message = appException.ErrorMessage,
                        Field = appException.Field
                    }
                ]);

            httpContext.Response.StatusCode = appException.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
            return true;
        }
    }
}
