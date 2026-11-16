namespace Yakku.Application.Auth
{
    public static class OtpOptions
    {
        /// <summary>
        /// How long an OTP is valid, in minutes. The one place this is defined (not an environment variable): it
        /// drives the Redis TTL and the <c>expiresInSeconds</c> the client sees. The SMS template states no expiry.
        /// </summary>
        public const int ExpiryMinutes = 5;

        public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(ExpiryMinutes);
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
        public const int MaxVerificationAttempts = 5;
        public const int Length = 6;
        public const int ExpiresInSeconds = ExpiryMinutes * 60;

        /// <summary>Per phone number: at most this many OTP SMS inside <see cref="SendWindow"/>.</summary>
        public const int MaxSendsPerWindow = 5;
        public static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);
    }
}
