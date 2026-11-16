namespace Yakku.Application.Auth
{
    public static class OtpOptions
    {
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
        public const int MaxVerificationAttempts = 5;
        public const int Length = 6;
        public const int ExpiresInSeconds = 300;

        /// <summary>Per phone number: at most this many OTP SMS inside <see cref="SendWindow"/>.</summary>
        public const int MaxSendsPerWindow = 5;
        public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);
    }
}
