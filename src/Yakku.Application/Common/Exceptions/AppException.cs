namespace Yakku.Application.Common.Exceptions
{
    public class AppException : Exception
    {
        public int StatusCode { get; }
        public string ErrorCode { get; }
        public string? Field { get; }
        public string ErrorMessage { get; }

        public AppException(
            int statusCode,
            string errorCode,
            string message,
            string? field = null,
            string? errorMessage = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Field = field;
            ErrorMessage = errorMessage ?? message;
        }
    }
}
