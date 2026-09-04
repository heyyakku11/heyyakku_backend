namespace Yakku.Application.Common.Responses
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<ApiError>? Errors { get; set; }
        public PaginationMeta? Meta { get; set; }
    }

    public static class ApiResponse
    {
        public static ApiResponse<T> Ok<T>(T data, string message, PaginationMeta? meta = null)
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Errors = null,
                Meta = meta
            };
        }

        public static ApiResponse<T> Fail<T>(string message, IReadOnlyList<ApiError> errors)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                Errors = errors.ToList(),
                Meta = null
            };
        }

        public static ApiResponse<object> Fail(string message, IReadOnlyList<ApiError> errors)
        {
            return Fail<object>(message, errors);
        }

        public static ApiResponse<T> NotFound<T>(string message)
        {
            return Fail<T>(
                message,
                [
                    new ApiError
                    {
                        Code = ApiErrorCodes.NotFound,
                        Message = message
                    }
                ]);
        }
    }
}
