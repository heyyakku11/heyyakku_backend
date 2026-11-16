namespace Yakku.Application.Auth
{
    /// <summary>
    /// How long an OTP lives and how often one may be sent. The defaults come from <see cref="OtpOptions"/>.
    /// An OTP lives <see cref="OtpOptions.ExpiryMinutes"/> minutes, and never longer: <see cref="Ttl"/> rejects any
    /// value above that, so no policy (and no configuration) can make an OTP last longer. It is the single source
    /// for the Redis expiry and the <c>expiresInSeconds</c> the client sees.
    /// </summary>
    public sealed record OtpPolicy
    {
        public static readonly OtpPolicy Default = new();

        private readonly TimeSpan _ttl = OtpOptions.Ttl;

        /// <summary>Defaults to the maximum; a shorter lifetime is allowed (tests), a longer one throws.</summary>
        public TimeSpan Ttl
        {
            get => _ttl;
            init => _ttl = value > TimeSpan.Zero && value <= OtpOptions.Ttl
                ? value
                : throw new ArgumentOutOfRangeException(
                    nameof(value),
                    $"An OTP must live more than zero and at most {OtpOptions.ExpiryMinutes} minutes.");
        }

        public TimeSpan ResendCooldown { get; init; } = OtpOptions.ResendCooldown;

        /// <summary>Most OTPs one phone number may be sent inside <see cref="SendWindow"/>.</summary>
        public int MaxSendsPerWindow { get; init; } = OtpOptions.MaxSendsPerWindow;

        public TimeSpan SendWindow { get; init; } = OtpOptions.SendWindow;

        public int MaxVerificationAttempts { get; init; } = OtpOptions.MaxVerificationAttempts;

        public int ExpiresInSeconds => (int)Math.Ceiling(Ttl.TotalSeconds);
    }
}
