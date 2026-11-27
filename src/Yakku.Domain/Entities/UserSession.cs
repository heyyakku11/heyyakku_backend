namespace Yakku.Domain.Entities
{
    public class UserSession
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid? DeviceId { get; private set; }
        public string RefreshTokenHash { get; private set; } = string.Empty;
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }
        public DateTime? LastActiveAt { get; private set; }
        public DateTime CreatedAt { get; private set; }

        /// <summary>
        /// True for a website login: short-lived, never refreshed, and outside the per-user live session cap, so
        /// signing in on the web can never end a phone session.
        /// </summary>
        public bool IsWeb { get; private set; }

        public User User { get; private set; } = null!;
        public Device? Device { get; private set; }

        private UserSession()
        {
        }

        public UserSession(Guid userId, string refreshTokenHash, DateTime expiresAt, bool isWeb = false)
        {
            Id = Guid.NewGuid();
            IsWeb = isWeb;
            UserId = userId;
            RefreshTokenHash = refreshTokenHash;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            LastActiveAt = CreatedAt;
        }

        public void Rotate(string refreshTokenHash, DateTime expiresAt)
        {
            RefreshTokenHash = refreshTokenHash;
            ExpiresAt = expiresAt;
            LastActiveAt = DateTime.UtcNow;
        }

        public void Revoke()
        {
            RevokedAt ??= DateTime.UtcNow;
        }

        /// <summary>
        /// Binds the session to the installation it was created on. History is never rewritten: once set, the
        /// device cannot change.
        /// </summary>
        public void AssignDevice(Guid deviceId)
        {
            if (deviceId == Guid.Empty)
            {
                throw new ArgumentException("Device id is required.", nameof(deviceId));
            }

            if (DeviceId == deviceId)
            {
                return;
            }

            if (DeviceId is not null)
            {
                throw new InvalidOperationException("A session stays bound to the installation it was created on.");
            }

            DeviceId = deviceId;
        }

        public bool IsLive(DateTime utcNow)
        {
            return RevokedAt is null && !IsExpired(utcNow);
        }

        public bool IsExpired(DateTime utcNow)
        {
            return utcNow >= ExpiresAt;
        }
    }
}
