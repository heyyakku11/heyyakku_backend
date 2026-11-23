namespace Yakku.Application.Auth.Sms
{
    /// <summary>In-process SMS OTP queue. Retries are immediate (no backoff), unlike email OTP.</summary>
    public static class OtpSmsQueueOptions
    {
        public const int Capacity = 200;
        public const int MaxAttempts = 3;
    }
}
