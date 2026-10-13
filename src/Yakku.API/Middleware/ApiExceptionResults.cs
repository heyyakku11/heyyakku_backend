using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal static class ApiExceptionResults
    {
        public static IActionResult From(AppException exception)
        {
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
            return new BadRequestObjectResult(
                ApiResponse.Fail(
                    "Validation failed",
                    ApiErrorMapper.FromValidationException(exception)));
        }
    }
}
