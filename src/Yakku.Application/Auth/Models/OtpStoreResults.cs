namespace Yakku.Application.Auth.Models
{
    public enum OtpIssueStatus
    {
        /// <summary>The challenge was stored and replaces any earlier one.</summary>
        Issued,

        /// <summary>The previous OTP is younger than the resend cooldown. Nothing was changed.</summary>
        CooldownActive,

        /// <summary>The phone number has used up its sends for the window. Nothing was changed.</summary>
        SendLimitReached
    }

    public sealed record OtpIssueResult(OtpIssueStatus Status, TimeSpan RetryAfter);

    public enum OtpVerificationStatus
    {
        /// <summary>The OTP matched and was consumed: it can never be used again.</summary>
        Verified,

        /// <summary>There is no OTP (never issued, expired, or already used).</summary>
        NotFound,

        /// <summary>Wrong OTP. The failed attempt was counted.</summary>
        Invalid,

        /// <summary>The attempt limit is reached; only a new OTP helps.</summary>
        AttemptsExceeded
    }

    /// <param name="Challenge">Set only when <paramref name="Status"/> is <see cref="OtpVerificationStatus.Verified"/>.</param>
    public sealed record OtpVerificationResult(OtpVerificationStatus Status, OtpChallenge? Challenge = null);
}
