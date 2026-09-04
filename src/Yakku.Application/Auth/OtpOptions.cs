namespace Yakku.Application.Auth
{
    public static class OtpOptions
    {
        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
        public const int MaxVerificationAttempts = 5;
        public const int Length = 6;
        public const int ExpiresInSeconds = 300;
    }
}
