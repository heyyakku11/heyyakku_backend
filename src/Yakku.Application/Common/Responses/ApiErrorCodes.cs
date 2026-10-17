namespace Yakku.Application.Common.Responses
{
    public static class ApiErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InternalServerError = "INTERNAL_SERVER_ERROR";
        public const string NotFound = "NOT_FOUND";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string Conflict = "CONFLICT";
        public const string DeviceConflict = "DEVICE_CONFLICT";
        public const string AlreadyVoted = "ALREADY_VOTED";
        public const string AlreadyUsed = "ALREADY_USED";
        public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
        public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
        public const string RateLimited = "RATE_LIMITED";
        public const string OtpNotFound = "OTP_NOT_FOUND";
        public const string OtpExpired = "OTP_EXPIRED";
        public const string OtpInvalid = "OTP_INVALID";
        public const string OtpAttemptsExceeded = "OTP_ATTEMPTS_EXCEEDED";
        public const string OtpResendCooldown = "OTP_RESEND_COOLDOWN";
    }
}
