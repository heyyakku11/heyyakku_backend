namespace Yakku.Application.Auth.Sms
{
    /// <summary>
    /// Delivers an OTP that the backend has already generated and stored. It never generates, stores or verifies
    /// OTPs, and it does not retry: a repeated send could text the user twice.
    /// </summary>
    public interface ISmsService
    {
        /// <summary>False when SMS delivery is switched off by configuration; every send then fails as <see cref="SmsFailureCategory.Disabled"/>.</summary>
        bool IsEnabled { get; }

        Task<SmsSendResult> SendOtpAsync(SmsOtpMessage message, CancellationToken cancellationToken = default);
    }

    /// <param name="PhoneNumber">E.164, as normalised by <see cref="PhoneNumberNormalizer"/>.</param>
    /// <param name="Otp">The plain OTP. Must never be logged.</param>
    /// <param name="CorrelationId">Ties log lines and audit rows of one delivery together (the challenge id).</param>
    public sealed record SmsOtpMessage(string PhoneNumber, string Otp, Guid CorrelationId)
    {
        // The OTP is a credential: keep it out of any accidental ToString() / logging of the message.
        public override string ToString() => $"SmsOtpMessage {{ CorrelationId = {CorrelationId} }}";
    }

    public enum SmsFailureCategory
    {
        None = 0,

        /// <summary>SMS delivery is switched off by configuration.</summary>
        Disabled,

        /// <summary>The number cannot be texted through this provider. Nothing was sent.</summary>
        InvalidRecipient,

        /// <summary>The provider refused our credentials (or blocked our address).</summary>
        Authentication,

        /// <summary>The provider does not know, or refused, the configured template / flow.</summary>
        Template,

        /// <summary>The provider rejected the request for another reason (4xx or an error body).</summary>
        Rejected,

        /// <summary>The provider throttled us (HTTP 429).</summary>
        ProviderRateLimited,

        /// <summary>The provider failed (HTTP 5xx / 408).</summary>
        ProviderError,

        /// <summary>A 2xx reply that does not say the request succeeded. Never treated as success.</summary>
        InvalidResponse,

        /// <summary>The provider could not be reached.</summary>
        Network,

        /// <summary>The provider did not answer in time.</summary>
        Timeout
    }

    public sealed class SmsSendResult
    {
        private SmsSendResult(
            SmsFailureCategory failure,
            int? providerHttpStatus,
            string? providerRequestId,
            int latencyMs)
        {
            Failure = failure;
            ProviderHttpStatus = providerHttpStatus;
            ProviderRequestId = providerRequestId;
            LatencyMs = latencyMs;
        }

        /// <summary>The provider accepted the request. That is acceptance, not proof the handset received it.</summary>
        public bool Accepted => Failure == SmsFailureCategory.None;

        public SmsFailureCategory Failure { get; }
        public int? ProviderHttpStatus { get; }
        public string? ProviderRequestId { get; }
        public int LatencyMs { get; }

        public static SmsSendResult Success(int? httpStatus, string? providerRequestId, int latencyMs)
            => new(SmsFailureCategory.None, httpStatus, providerRequestId, latencyMs);

        public static SmsSendResult Failed(SmsFailureCategory failure, int? httpStatus = null, int latencyMs = 0)
            => new(failure, httpStatus, null, latencyMs);
    }
}
