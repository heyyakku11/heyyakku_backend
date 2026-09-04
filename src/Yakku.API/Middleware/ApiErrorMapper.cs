using FluentValidation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Yakku.Application.Common.Responses;

namespace Yakku.API.Middleware
{
    internal static class ApiErrorMapper
    {
        public static List<ApiError> FromValidationException(ValidationException exception)
        {
            return exception.Errors
                .Select(error => new ApiError
                {
                    Code = ApiErrorCodes.ValidationError,
                    Message = error.ErrorMessage,
                    Field = ToCamelCase(error.PropertyName)
                })
                .ToList();
        }

        public static List<ApiError> FromModelState(ModelStateDictionary modelState)
        {
            var errors = modelState
                .Where(entry => entry.Value is { Errors.Count: > 0 })
                .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiError
                {
                    Code = ApiErrorCodes.ValidationError,
                    Message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid."
                        : error.ErrorMessage,
                    Field = ToCamelCase(entry.Key)
                }))
                .ToList();

            if (errors.Count == 0)
            {
                errors.Add(new ApiError
                {
                    Code = ApiErrorCodes.ValidationError,
                    Message = "Validation failed"
                });
            }

            return errors;
        }

        private static string? ToCamelCase(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (char.IsLower(value[0]))
            {
                return value;
            }

            return char.ToLowerInvariant(value[0]) + value[1..];
        }
    }
}
