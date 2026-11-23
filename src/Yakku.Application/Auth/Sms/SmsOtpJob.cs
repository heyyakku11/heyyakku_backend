namespace Yakku.Application.Auth.Sms
{
    /// <summary>
    /// One OTP SMS to deliver. Carries the plaintext OTP because Redis stores only the hash when SMS is enabled.
    /// Never log this record.
    /// </summary>
    public sealed record SmsOtpJob(Guid ChallengeId, string PhoneNumber, string Otp)
    {
        public override string ToString() => $"SmsOtpJob {{ ChallengeId = {ChallengeId} }}";
    }
}
