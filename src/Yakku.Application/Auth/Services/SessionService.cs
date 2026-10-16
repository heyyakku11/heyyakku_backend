using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class SessionService : ISessionService
    {
        private readonly IUserSessionRepository _sessions;
        private readonly ITokenService _tokenService;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;
        private readonly IAuditLogWriter _audit;

        public SessionService(
            IUserSessionRepository sessions,
            ITokenService tokenService,
            IValidator<RefreshTokenRequest> refreshTokenValidator,
            IAuditLogWriter audit)
        {
            _sessions = sessions;
            _tokenService = tokenService;
            _refreshTokenValidator = refreshTokenValidator;
            _audit = audit;
        }

        public async Task<TokenResponse> CreateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            const int maxLiveSessions = 5;
            var utcNow = DateTime.UtcNow;
            var liveSessions = await _sessions.ListLiveByUserIdAsync(userId, utcNow, cancellationToken);
            Guid? evictedSessionId = null;
            if (liveSessions.Count >= maxLiveSessions)
            {
                var leastActive = liveSessions
                    .OrderBy(session => session.LastActiveAt ?? DateTime.MinValue)
                    .ThenBy(session => session.CreatedAt)
                    .First();
                leastActive.Revoke();
                evictedSessionId = leastActive.Id;
            }

            var secret = RefreshTokenHasher.GenerateSecret();
            var session = new UserSession(
                userId,
                RefreshTokenHasher.Hash(string.Empty, secret),
                utcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);

            if (evictedSessionId is Guid evicted)
            {
                await AuditAsync(AuditEvents.Auth.SessionEvicted, AuditOutcome.Success, userId, evicted);
            }

            await AuditAsync(AuditEvents.Auth.SessionCreated, AuditOutcome.Success, userId, session.Id);

            return ToResponse(userId, session.Id, secret);
        }

        public async Task<TokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(
                refreshToken,
                AuditEvents.Auth.TokenRefreshFailed,
                cancellationToken);

            var secret = RefreshTokenHasher.GenerateSecret();
            session.Rotate(
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.Auth.TokenRefreshed, AuditOutcome.Success, session.UserId, session.Id);

            return ToResponse(session.UserId, session.Id, secret);
        }

        public async Task RevokeAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(
                refreshToken,
                AuditEvents.Auth.SessionRevokeFailed,
                cancellationToken);
            session.Revoke();
            await _sessions.SaveChangesAsync(cancellationToken);
            await AuditAsync(AuditEvents.Auth.SessionRevoked, AuditOutcome.Success, session.UserId, session.Id);
        }

        public async Task RevokeAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var sessions = await _sessions.ListByUserIdAsync(userId, cancellationToken);
            foreach (var session in sessions)
            {
                session.Revoke();
            }

            await _sessions.SaveChangesAsync(cancellationToken);
            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    AuditEvents.Auth.AllSessionsRevoked,
                    AuditOutcome.Success,
                    Metadata: new Dictionary<string, string?> { ["sessionCount"] = sessions.Count.ToString() }),
                CancellationToken.None);
        }

        private async Task<UserSession> LoadValidSessionAsync(
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
                // Presenting a token of a revoked session means a token that should be dead is still in use.
                var revoked = session?.RevokedAt is not null;
                await AuditFailureAsync(
                    failureEvent,
                    session is null ? "session_not_found" : revoked ? "session_revoked" : "session_expired",
                    revoked ? AuditOutcome.Suspicious : AuditOutcome.Failure,
                    session?.UserId,
                    sessionId);

                if (session is not null && session.RevokedAt is null && session.IsExpired(DateTime.UtcNow))
                {
                    await _sessions.DeleteAsync(session, cancellationToken);
                    await _sessions.SaveChangesAsync(cancellationToken);
                }

                throw Unauthorized();
            }

            if (!RefreshTokenHasher.Verify(string.Empty, secret, session.RefreshTokenHash))
            {
                // Right session id, wrong secret: typically a stale token replayed after rotation, or a guess.
                await AuditFailureAsync(
                    failureEvent,
                    "secret_mismatch",
                    AuditOutcome.Suspicious,
                    session.UserId,
                    session.Id);
                throw Unauthorized();
            }

            return session;
        }

        private Task AuditAsync(string eventType, AuditOutcome outcome, Guid userId, Guid sessionId)
        {
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    eventType,
                    outcome,
                    SessionId: sessionId),
                CancellationToken.None);
        }

        private Task AuditFailureAsync(
            string eventType,
            string reason,
            AuditOutcome outcome,
            Guid? userId,
            Guid? sessionId)
        {
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    eventType,
                    outcome,
                    FailureReason: reason,
                    SessionId: sessionId),
                CancellationToken.None);
        }

        private TokenResponse ToResponse(Guid userId, Guid sessionId, string secret)
        {
            return new TokenResponse
            {
                AccessToken = _tokenService.CreateAccessToken(userId, sessionId),
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
