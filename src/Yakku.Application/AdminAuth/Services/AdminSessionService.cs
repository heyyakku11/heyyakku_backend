using System.Net;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.Audit;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Services
{
    public class AdminSessionService : IAdminSessionService
    {
        private readonly IAdminSessionRepository _sessions;
        private readonly AdminSessionOptions _options;
        private readonly IAuditLogWriter _audit;
        private readonly TimeProvider _time;

        public AdminSessionService(
            IAdminSessionRepository sessions,
            AdminSessionOptions options,
            IAuditLogWriter audit,
            TimeProvider time)
        {
            _sessions = sessions;
            _options = options;
            _audit = audit;
            _time = time;
        }

        public async Task<AdminSessionIssue> CreateAsync(
            Guid adminId,
            AdminClientInfo client,
            CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var token = AdminSessionToken.Generate();
            var absolute = now.Add(_options.AbsoluteTimeout);
            var session = new AdminSession(
                adminId,
                AdminSessionToken.Hash(token),
                now,
                Min(now.Add(_options.IdleTimeout), absolute),
                absolute,
                IPAddress.TryParse(client.IpAddress, out var ip) ? ip : null,
                Truncate(client.UserAgent, 512));

            await _sessions.AddAsync(session, cancellationToken);
            await AuditAsync(AuditEvents.Auth.SessionCreated, AuditOutcome.Success, adminId, session.Id, null);

            return new AdminSessionIssue(session.Id, token, absolute);
        }

        public async Task<AdminSessionAuthResult> AuthenticateAsync(
            string? token,
            AdminClientInfo client,
            CancellationToken cancellationToken = default)
        {
            if (!AdminSessionToken.HasValidShape(token))
            {
                // Missing cookie is normal (not logged in). A present but malformed one is worth a record.
                if (!string.IsNullOrEmpty(token))
                {
                    await AuditAsync(AuditEvents.Auth.SessionRejected, AuditOutcome.Failure, null, null, "malformed_token");
                }

                return AdminSessionAuthResult.Fail(string.IsNullOrEmpty(token) ? "missing_token" : "malformed_token");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var hash = AdminSessionToken.Hash(token!);
            var session = await _sessions.FindByHashAsync(hash, now, cancellationToken);
            if (session is null)
            {
                await AuditAsync(AuditEvents.Auth.SessionRejected, AuditOutcome.Failure, null, null, "session_not_found");
                return AdminSessionAuthResult.Fail("session_not_found");
            }

            if (session.IsRevoked)
            {
                // A revoked token coming back is replay of a stolen or stale cookie.
                await AuditAsync(AuditEvents.Auth.SessionRejected, AuditOutcome.Suspicious, session.AdminId, session.Id, "session_revoked");
                return AdminSessionAuthResult.Fail("session_revoked");
            }

            if (session.IsExpired(now))
            {
                var reason = now >= session.AbsoluteExpiresAt
                    ? AdminSessionRevocationReasons.AbsoluteTimeout
                    : AdminSessionRevocationReasons.IdleTimeout;
                await _sessions.RevokeAsync(session.AdminId, session.Id, reason, now, cancellationToken);
                await AuditAsync(AuditEvents.Auth.SessionExpired, AuditOutcome.Failure, session.AdminId, session.Id, reason);
                return AdminSessionAuthResult.Fail(reason);
            }

            var admin = session.Admin;
            if (admin is null || !admin.IsVerified || admin.Status != AdminStatus.Active)
            {
                // A suspended admin's existing sessions stop working at once, whatever their lifetime.
                await _sessions.RevokeAllAsync(session.AdminId, AdminSessionRevocationReasons.AccountDisabled, now, cancellationToken);
                await AuditAsync(AuditEvents.Auth.SessionRejected, AuditOutcome.Denied, session.AdminId, session.Id, "admin_not_active");
                return AdminSessionAuthResult.Fail("admin_not_active");
            }

            var idleExpiresAt = Min(now.Add(_options.IdleTimeout), session.AbsoluteExpiresAt);
            string? rotatedToken = null;

            // Only the current token rotates. The previous one (grace window) just rides along.
            var usedCurrentToken = string.Equals(session.SessionHash, hash, StringComparison.Ordinal);
            if (usedCurrentToken && now - session.RotatedAt >= _options.RotationInterval)
            {
                var newToken = AdminSessionToken.Generate();
                var rotated = await _sessions.TryRotateAsync(
                    session.Id,
                    hash,
                    AdminSessionToken.Hash(newToken),
                    now,
                    now.Add(_options.RotationGrace),
                    idleExpiresAt,
                    cancellationToken);

                // Losing the race is fine: the winning request hands the browser the new cookie.
                if (rotated)
                {
                    rotatedToken = newToken;
                    await AuditAsync(AuditEvents.Auth.SessionRotated, AuditOutcome.Success, admin.Id, session.Id, null);
                }
            }
            else if (now - session.LastActivityAt >= _options.ActivityWriteInterval)
            {
                await _sessions.TouchAsync(session.Id, now, idleExpiresAt, cancellationToken);
            }

            return new AdminSessionAuthResult
            {
                Succeeded = true,
                AdminId = admin.Id,
                SessionId = session.Id,
                Email = admin.Email,
                Role = admin.Role,
                RotatedToken = rotatedToken,
                AbsoluteExpiresAt = session.AbsoluteExpiresAt
            };
        }

        public async Task RevokeAsync(
            Guid adminId,
            Guid sessionId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            // Scoped to the caller's own admin id in the SQL itself, so one admin can never end another's session.
            if (await _sessions.RevokeAsync(adminId, sessionId, reason, _time.GetUtcNow().UtcDateTime, cancellationToken))
            {
                await AuditAsync(AuditEvents.Auth.SessionRevoked, AuditOutcome.Success, adminId, sessionId, reason);
            }
        }

        public async Task RevokeAllAsync(
            Guid adminId,
            string reason,
            CancellationToken cancellationToken = default)
        {
            var count = await _sessions.RevokeAllAsync(adminId, reason, _time.GetUtcNow().UtcDateTime, cancellationToken);
            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    AuditEvents.Auth.AllSessionsRevoked,
                    AuditOutcome.Success,
                    FailureReason: reason,
                    Metadata: new Dictionary<string, string?> { ["revoked"] = count.ToString() }),
                CancellationToken.None);
        }

        private Task AuditAsync(string eventType, AuditOutcome outcome, Guid? adminId, Guid? sessionId, string? reason)
        {
            // Never the token, its hash or the cookie: only ids and a reason code.
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    eventType,
                    outcome,
                    FailureReason: reason,
                    SessionId: sessionId),
                CancellationToken.None);
        }

        private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

        private static string? Truncate(string? value, int max) =>
            value is null ? null : value.Length <= max ? value : value[..max];
    }
}
