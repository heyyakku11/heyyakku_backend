using FluentValidation;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Services
{
    public class AdminSessionService : IAdminSessionService
    {
        private readonly IAdminSessionRepository _sessions;
        private readonly IAdminRepository _admins;
        private readonly ITokenService _tokenService;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;
        private readonly IAuditLogWriter _audit;

        public AdminSessionService(
            IAdminSessionRepository sessions,
            IAdminRepository admins,
            ITokenService tokenService,
            IValidator<RefreshTokenRequest> refreshTokenValidator,
            IAuditLogWriter audit)
        {
            _sessions = sessions;
            _admins = admins;
            _tokenService = tokenService;
            _refreshTokenValidator = refreshTokenValidator;
            _audit = audit;
        }

        public async Task<TokenResponse> CreateAsync(
            Guid adminId,
            string email,
            CancellationToken cancellationToken = default)
        {
            var existing = await _sessions.GetByAdminIdAsync(adminId, cancellationToken);
            if (existing is not null)
            {
                await _sessions.DeleteAsync(existing, cancellationToken);
                await _sessions.SaveChangesAsync(cancellationToken);
                await AuditAsync(AuditEvents.Auth.SessionEvicted, AuditOutcome.Success, adminId, existing.Id);
            }

            var secret = RefreshTokenHasher.GenerateSecret();
            var session = new AdminSession(
                adminId,
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.Auth.SessionCreated, AuditOutcome.Success, adminId, session.Id);

            return ToResponse(adminId, email, session.Id, secret);
        }

        public async Task<TokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(
                refreshToken,
                AuditEvents.Auth.TokenRefreshFailed,
                cancellationToken);
            var admin = await _admins.GetByIdAsync(session.AdminId, cancellationToken);
            if (admin is null || admin.Status == AdminStatus.Disabled)
            {
                await AuditFailureAsync(
                    AuditEvents.Auth.TokenRefreshFailed,
                    admin is null ? "admin_not_found" : "admin_disabled",
                    AuditOutcome.Failure,
                    session.AdminId,
                    session.Id);
                throw Unauthorized();
            }

            var secret = RefreshTokenHasher.GenerateSecret();
            session.Rotate(
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.Auth.TokenRefreshed, AuditOutcome.Success, admin.Id, session.Id);

            return ToResponse(admin.Id, admin.Email, session.Id, secret);
        }

        public async Task RevokeAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(
                refreshToken,
                AuditEvents.Auth.SessionRevokeFailed,
                cancellationToken);
            var adminId = session.AdminId;
            var sessionId = session.Id;
            await _sessions.DeleteAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.Auth.SessionRevoked, AuditOutcome.Success, adminId, sessionId);
        }

        public async Task RevokeAllAsync(
            Guid adminId,
            CancellationToken cancellationToken = default)
        {
            await _sessions.DeleteAllByAdminIdAsync(adminId, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    AuditEvents.Auth.AllSessionsRevoked,
                    AuditOutcome.Success),
                CancellationToken.None);
        }

        private async Task<AdminSession> LoadValidSessionAsync(
            string refreshToken,
            string failureEvent,
            CancellationToken cancellationToken)
        {
            var request = new RefreshTokenRequest { RefreshToken = refreshToken };
            await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!RefreshTokenHasher.TryParse(refreshToken.Trim(), out var sessionId, out var secret))
            {
                await AuditFailureAsync(failureEvent, "malformed_token", AuditOutcome.Failure, null, null);
                throw Unauthorized();
            }

            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is null || session.RevokedAt is not null || session.IsExpired(DateTime.UtcNow))
            {
                var revoked = session?.RevokedAt is not null;
                await AuditFailureAsync(
                    failureEvent,
                    session is null ? "session_not_found" : revoked ? "session_revoked" : "session_expired",
                    revoked ? AuditOutcome.Suspicious : AuditOutcome.Failure,
                    session?.AdminId,
                    sessionId);

                if (session is not null)
                {
                    await _sessions.DeleteAsync(session, cancellationToken);
                    await _sessions.SaveChangesAsync(cancellationToken);
                }

                throw Unauthorized();
            }

            if (!RefreshTokenHasher.Verify(string.Empty, secret, session.RefreshTokenHash))
            {
                await AuditFailureAsync(
                    failureEvent,
                    "secret_mismatch",
                    AuditOutcome.Suspicious,
                    session.AdminId,
                    session.Id);
                throw Unauthorized();
            }

            return session;
        }

        private Task AuditAsync(string eventType, AuditOutcome outcome, Guid adminId, Guid sessionId)
        {
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.Admin,
                    adminId,
                    eventType,
                    outcome,
                    SessionId: sessionId),
                CancellationToken.None);
        }

        private Task AuditFailureAsync(
            string eventType,
            string reason,
            AuditOutcome outcome,
            Guid? adminId,
            Guid? sessionId)
        {
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

        private TokenResponse ToResponse(Guid adminId, string email, Guid sessionId, string secret)
        {
            return new TokenResponse
            {
                AccessToken = _tokenService.CreateAdminAccessToken(adminId, sessionId, email),
                RefreshToken = $"{sessionId}.{secret}",
                AccessTokenExpiresInSeconds = JwtOptions.AccessTokenExpiresInSeconds,
                RefreshTokenExpiresInSeconds = JwtOptions.RefreshTokenExpiresInSeconds
            };
        }

        private static AppException Unauthorized()
        {
            return new AppException(
                401,
                ApiErrorCodes.Unauthorized,
                "Invalid or expired refresh token.",
                "refreshToken");
        }
    }
}
