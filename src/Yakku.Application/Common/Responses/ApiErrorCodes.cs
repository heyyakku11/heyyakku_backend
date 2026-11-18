namespace Yakku.Application.Common.Responses
{
    public static class ApiErrorCodes
    {
        public const string ValidationError = "VALIDATION_ERROR";
        public const string InternalServerError = "INTERNAL_SERVER_ERROR";
        public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
        public const string NotFound = "NOT_FOUND";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string Conflict = "CONFLICT";
        public const string DeviceConflict = "DEVICE_CONFLICT";
        public const string AlreadyVoted = "ALREADY_VOTED";
        public const string AlreadyUsed = "ALREADY_USED";
        public const string RegistrationExpired = "REGISTRATION_EXPIRED";
        public const string InvalidSecretCode = "INVALID_SECRET_CODE";
        public const string InvalidToken = "INVALID_TOKEN";
        public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";
        public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
        public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
        public const string RateLimited = "RATE_LIMITED";
        public const string AnonymousNameUnavailable = "ANONYMOUS_NAME_UNAVAILABLE";
        public const string OtpNotFound = "OTP_NOT_FOUND";
        public const string OtpExpired = "OTP_EXPIRED";
        public const string OtpInvalid = "OTP_INVALID";
        public const string OtpAttemptsExceeded = "OTP_ATTEMPTS_EXCEEDED";
        public const string OtpResendCooldown = "OTP_RESEND_COOLDOWN";
        public const string AskBackNotAllowed = "ASK_BACK_NOT_ALLOWED";
        public const string AskBackNotActive = "ASK_BACK_NOT_ACTIVE";

        /// <summary>It is not the caller's turn to send in this conversation.</summary>
        public const string AskBackNotYourTurn = "ASK_BACK_NOT_YOUR_TURN";

        /// <summary>Every question the plan allows was asked and answered.</summary>
        public const string AskBackLimitReached = "ASK_BACK_LIMIT_REACHED";

        /// <summary>The card is not unlocked for Ask Back (and the free ask back cannot be used on this reason).</summary>
        public const string AskBackLocked = "ASK_BACK_LOCKED";

        /// <summary>The plan cannot unlock the card: no ticket is left, or there is no active plan.</summary>
        public const string AskBackUnlockRequired = "ASK_BACK_UNLOCK_REQUIRED";

        /// <summary>The conversation was unlocked through a plan that has expired, so nobody can send.</summary>
        public const string AskBackPlanExpired = "ASK_BACK_PLAN_EXPIRED";
    }
}
