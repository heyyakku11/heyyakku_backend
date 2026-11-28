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

        /// <summary>Client app version is below the minimum supported version (reserved for a future hard gate).</summary>
        public const string ForceUpdateRequired = "FORCE_UPDATE_REQUIRED";

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

        /// <summary>
        /// A conversation top-up cannot be bought here: not the asker, the chat is blocked or closed, the card is not
        /// unlocked or paused, or the chat still has questions left.
        /// </summary>
        public const string AskBackTopUpNotAvailable = "ASK_BACK_TOP_UP_NOT_AVAILABLE";

        /// <summary>The caller already has this paid plan active.</summary>
        public const string PlanAlreadyActive = "PLAN_ALREADY_ACTIVE";

        /// <summary>Active Love cannot be replaced with Lite.</summary>
        public const string PlanDowngradeNotAllowed = "PLAN_DOWNGRADE_NOT_ALLOWED";

        /// <summary>Permanent access to this card was already purchased.</summary>
        public const string CardAlreadyUnlocked = "CARD_ALREADY_UNLOCKED";

        /// <summary>Razorpay is disabled or not configured for this environment.</summary>
        public const string PaymentProviderDisabled = "PAYMENT_PROVIDER_DISABLED";

        /// <summary>The payment gateway rejected or failed the request.</summary>
        public const string PaymentGatewayError = "PAYMENT_GATEWAY_ERROR";

        /// <summary>Razorpay Checkout signature did not match.</summary>
        public const string PaymentSignatureInvalid = "PAYMENT_SIGNATURE_INVALID";

        /// <summary>Payment is not captured (or failed), so access was not granted.</summary>
        public const string PaymentNotCaptured = "PAYMENT_NOT_CAPTURED";

        /// <summary>Razorpay payment does not belong to the expected order.</summary>
        public const string PaymentOrderMismatch = "PAYMENT_ORDER_MISMATCH";

        /// <summary>Razorpay payment amount does not match the internal order.</summary>
        public const string PaymentAmountMismatch = "PAYMENT_AMOUNT_MISMATCH";

        /// <summary>Razorpay payment currency does not match the internal order.</summary>
        public const string PaymentCurrencyMismatch = "PAYMENT_CURRENCY_MISMATCH";

        /// <summary>Razorpay webhook HMAC did not match the raw body.</summary>
        public const string WebhookSignatureInvalid = "WEBHOOK_SIGNATURE_INVALID";

        /// <summary>Razorpay API call failed after the request was accepted locally.</summary>
        public const string PaymentProviderError = "PAYMENT_PROVIDER_ERROR";
    }
}
