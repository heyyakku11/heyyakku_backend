using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public sealed record AdminClientInfo(string? IpAddress, string? UserAgent);

    /// <summary>A freshly created session. <see cref="Token"/> goes into the cookie once and is never returned in a body.</summary>
    public sealed record AdminSessionIssue(Guid SessionId, string Token, DateTime AbsoluteExpiresAt);

    public sealed class AdminSessionAuthResult
    {
        public bool Succeeded { get; init; }
        public string? FailureReason { get; init; }
        public Guid AdminId { get; init; }
        public Guid SessionId { get; init; }
        public string Email { get; init; } = string.Empty;
        public AdminRole Role { get; init; }

        /// <summary>Set when the token was rotated on this request: the cookie must be replaced with it.</summary>
        public string? RotatedToken { get; init; }
        public DateTime AbsoluteExpiresAt { get; init; }

        public static AdminSessionAuthResult Fail(string reason) => new() { FailureReason = reason };
    }

    public interface IAdminSessionService
    {
        /// <summary>Starts a new session with a brand-new token. A token supplied by the client is never reused (no session fixation).</summary>
        Task<AdminSessionIssue> CreateAsync(Guid adminId, AdminClientInfo client, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks the cookie token against the server: exists, not revoked, not expired, admin still active.
        /// Renews activity and rotates the token when due. Never throws for a bad token; it returns a failure.
        /// </summary>
        Task<AdminSessionAuthResult> AuthenticateAsync(string? token, AdminClientInfo client, CancellationToken cancellationToken = default);

        Task RevokeAsync(Guid adminId, Guid sessionId, string reason, CancellationToken cancellationToken = default);

        Task RevokeAllAsync(Guid adminId, string reason, CancellationToken cancellationToken = default);
    }

    public static class AdminSessionRevocationReasons
    {
        public const string Logout = "logout";
        public const string LogoutAll = "logout_all";
        public const string PasswordChanged = "password_changed";
        public const string PasswordReset = "password_reset";
        public const string AccountDisabled = "account_disabled";
        public const string IdleTimeout = "idle_timeout";
        public const string AbsoluteTimeout = "absolute_timeout";
    }
}
