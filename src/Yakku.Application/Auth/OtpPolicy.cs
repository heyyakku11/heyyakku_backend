namespace Yakku.Application.Auth
{
    /// <summary>
    /// How long an OTP lives and how often one may be sent. The TTL is the single source for the Redis expiry,
    /// the "valid for N minutes" in the SMS, and the <c>expiresInSeconds</c> the client sees.
    /// </summary>
    public sealed record OtpPolicy
    {
        /// <summary>The longest OTP validity that can be stated in whole minutes in the SMS.</summary>
        public const int MaxExpiryMinutes = 30;

        public static readonly OtpPolicy Default = new();

        public TimeSpan Ttl { get; init; } = OtpOptions.Ttl;

        public TimeSpan ResendCooldown { get; init; } = OtpOptions.ResendCooldown;

        /// <summary>Most OTPs one phone number may be sent inside <see cref="SendWindow"/>.</summary>
        public int MaxSendsPerWindow { get; init; } = OtpOptions.MaxSendsPerWindow;

        public TimeSpan SendWindow { get; init; } = OtpOptions.SendWindow;

        public int MaxVerificationAttempts { get; init; } = OtpOptions.MaxVerificationAttempts;

        public int ExpiryMinutes => (int)Math.Ceiling(Ttl.TotalMinutes);

        public int ExpiresInSeconds => (int)Math.Ceiling(Ttl.TotalSeconds);

        public static OtpPolicy ForExpiryMinutes(int minutes)
        {
            if (minutes < 1 || minutes > MaxExpiryMinutes)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minutes),
                    $"OTP expiry must be between 1 and {MaxExpiryMinutes} minutes.");
            }

            return new OtpPolicy { Ttl = TimeSpan.FromMinutes(minutes) };
        }
    }
}
