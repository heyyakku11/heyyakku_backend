namespace Yakku.Application.AdminAuth.Interfaces
{
    /// <summary>A registration waiting for the secret code and the email link.</summary>
    public sealed class PendingAdminRegistration
    {
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public bool SecretConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Short-lived admin auth state (pending registrations and single-use link tokens).</summary>
    public interface IAdminAuthStore
    {
        Task<PendingAdminRegistration?> GetPendingAsync(string email, CancellationToken cancellationToken = default);

        Task SetPendingAsync(
            string email,
            PendingAdminRegistration pending,
            TimeSpan ttl,
            CancellationToken cancellationToken = default);

        /// <summary>Overwrites the pending record without extending its lifetime; false when it has expired.</summary>
        Task<bool> ReplacePendingKeepingTtlAsync(
            string email,
            PendingAdminRegistration pending,
            CancellationToken cancellationToken = default);

        Task DeletePendingAsync(string email, CancellationToken cancellationToken = default);

        Task SetVerificationTokenAsync(
            string tokenHash,
            string email,
            TimeSpan ttl,
            CancellationToken cancellationToken = default);

        /// <summary>Atomically reads and removes the token, so a link works once. Returns the email, or null.</summary>
        Task<string?> ConsumeVerificationTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>Stores the reset token for the admin, invalidating any earlier one.</summary>
        Task SetResetTokenAsync(
            Guid adminId,
            string tokenHash,
            TimeSpan ttl,
            CancellationToken cancellationToken = default);

        /// <summary>Atomically reads and removes the reset token. Returns the admin id, or null.</summary>
        Task<Guid?> ConsumeResetTokenAsync(string tokenHash, CancellationToken cancellationToken = default);

        /// <summary>True when the caller may send another forgot-password email (and starts the cooldown).</summary>
        Task<bool> TryStartForgotPasswordCooldownAsync(
            string email,
            TimeSpan cooldown,
            CancellationToken cancellationToken = default);
    }

    public interface IAdminAuthSettings
    {
        /// <summary>The ADMIN_SECRET_CODE required to complete a registration.</summary>
        string SecretCode { get; }

        /// <summary>The web app base URL (WEB_BASE_URL) used to build email links, without a trailing slash.</summary>
        string WebBaseUrl { get; }
    }
}
