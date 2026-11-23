using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal static class ApiExceptionResults
    {
        public static IActionResult From(AppException exception)
        {
            return From(null, exception);
        }

        public static IActionResult From(HttpContext? httpContext, AppException exception)
        {
            WriteSystemLog(httpContext, exception);

            var response = ApiResponse.Fail(
                exception.Message,
                [
                    new ApiError
                    {
                        Code = exception.ErrorCode,
                        Message = exception.ErrorMessage,
                        Field = exception.Field
                    }
                ]);

            return new ObjectResult(response)
            {
                StatusCode = exception.StatusCode
            };
        }

        public static IActionResult From(ValidationException exception)
        {
            return From(null, exception);
        }

        public static IActionResult From(HttpContext? httpContext, ValidationException exception)
        {
            WriteSystemLog(httpContext, exception);

            return new BadRequestObjectResult(
                ApiResponse.Fail(
                    "Validation failed",
                    ApiErrorMapper.FromValidationException(exception)));
        }

        private static void WriteSystemLog(HttpContext? httpContext, Exception exception)
        {
            if (httpContext is null)
            {
                return;
            }

            var audit = httpContext.RequestServices.GetService<IAuditLogWriter>();
            if (audit is null)
            {
                return;
            }

            _ = ExceptionSystemLog.WriteAsync(
                audit,
                exception,
                nameof(ApiExceptionResults),
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                cancellationToken: CancellationToken.None);
        }
    }
}
