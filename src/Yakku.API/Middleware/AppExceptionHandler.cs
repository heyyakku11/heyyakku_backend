using Microsoft.AspNetCore.Diagnostics;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal sealed class AppExceptionHandler : IExceptionHandler
    {
        private readonly IAuditLogWriter _audit;

        public AppExceptionHandler(IAuditLogWriter audit)
        {
            _audit = audit;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is not AppException appException)
            {
                return false;
            }

            await ExceptionSystemLog.WriteAsync(
                _audit,
                appException,
                nameof(AppExceptionHandler),
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                appException.StatusCode,
                cancellationToken: CancellationToken.None);

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
