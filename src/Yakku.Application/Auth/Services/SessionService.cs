using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class SessionService : ISessionService
    {
        private const int MaxLiveSessions = 5;

        private readonly IUserSessionRepository _sessions;
        private readonly ITokenService _tokenService;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;
        private readonly IDeviceRepository _devices;
        private readonly IDeviceSessionBinder _deviceBinder;
        private readonly IAuditLogWriter _audit;

        public SessionService(
            IUserSessionRepository sessions,
            ITokenService tokenService,
            IValidator<RefreshTokenRequest> refreshTokenValidator,
            IDeviceRepository devices,
            IDeviceSessionBinder deviceBinder,
            IAuditLogWriter audit)
        {
            _sessions = sessions;
            _tokenService = tokenService;
            _refreshTokenValidator = refreshTokenValidator;
            _devices = devices;
            _deviceBinder = deviceBinder;
            _audit = audit;
        }

        public Task<TokenResponse> CreateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return CreateCoreAsync(userId, null, cancellationToken);
        }

        public async Task<TokenResponse> CreateAsync(
            Guid userId,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken = default)
        {
            if (installation is null)
            {
                return await CreateCoreAsync(userId, null, cancellationToken);
            }

            var failure = "bind_failed";
            for (var attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    return await CreateCoreAsync(userId, installation, cancellationToken);
                }
                catch (AppException exception)
                    when (attempt == 0 && exception.ErrorCode == ApiErrorCodes.DeviceConflict)
                {
                    // Lost a unique-index race with a concurrent request for the same installation or token;
                    // the transaction is rolled back, so run once more against the winner's row.
                    failure = exception.ErrorCode;
                }
                catch (Exception exception) when (exception is AppException or ValidationException)
                {
                    failure = exception is AppException app ? app.ErrorCode : "validation";
                    break;
                }
            }

            // The OTP is already consumed, so a device problem must never lock the user out. The session is created
            // unbound; the client binds the installation afterwards through POST /devices/register.
            await AuditFailureAsync(AuditEvents.Device.LoginBindFailed, failure, AuditOutcome.Failure, userId, null);
            return await CreateCoreAsync(userId, null, cancellationToken);
        }

        /// <summary>
        /// Creates the session, evicts the oldest ones over the cap (releasing their installations) and, when an
        /// installation is given, binds it to the new session, all in one transaction. The user row is locked, so
        /// concurrent logins of the same user cannot both slip under the session cap.
        /// </summary>
        private async Task<TokenResponse> CreateCoreAsync(
            Guid userId,
            RegisterDeviceRequest? installation,
            CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var secret = RefreshTokenHasher.GenerateSecret();
            var session = new UserSession(
                userId,
                RefreshTokenHasher.Hash(string.Empty, secret),
                utcNow.Add(JwtOptions.RefreshTokenLifetime));
            var evictedSessionIds = new List<Guid>();
            DeviceLoginBindResult? bound = null;

            await _devices.ExecuteInTransactionAsync(async transactionToken =>
            {
                evictedSessionIds.Clear();
                bound = null;

                await _sessions.LockUserAsync(userId, transactionToken);
                var liveSessions = await _sessions.ListLiveByUserIdAsync(userId, utcNow, transactionToken);
                var overflow = liveSessions.Count - (MaxLiveSessions - 1);
                if (overflow > 0)
                {
                    foreach (var evicted in liveSessions
                                 .OrderBy(live => live.LastActiveAt ?? DateTime.MinValue)
                                 .ThenBy(live => live.CreatedAt)
                                 .Take(overflow))
                    {
                        evicted.Revoke();
                        evictedSessionIds.Add(evicted.Id);
                    }
                }

                await _sessions.AddAsync(session, transactionToken);
                await _sessions.SaveChangesAsync(transactionToken);

                if (evictedSessionIds.Count > 0)
                {
                    // An installation whose session was evicted no longer belongs to that account.
                    await _devices.DetachBySessionsAsync(evictedSessionIds, transactionToken);
                }

                if (installation is not null)
                {
                    bound = await _deviceBinder.BindLoginAsync(userId, session, installation, transactionToken);
                }
            }, cancellationToken);

            foreach (var evictedId in evictedSessionIds)
            {
                await AuditAsync(AuditEvents.Auth.SessionEvicted, AuditOutcome.Success, userId, evictedId);
            }

            await AuditAsync(AuditEvents.Auth.SessionCreated, AuditOutcome.Success, userId, session.Id);
            if (bound is not null)
            {
                await AuditAsync(AuditEvents.Device.LoginBound, AuditOutcome.Success, userId, session.Id);
            }

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
            var utcNow = DateTime.UtcNow;

            // Compare-and-swap on the stored hash: of two concurrent refreshes with the same token only one wins,
            // and a refresh racing a logout cannot resurrect the session.
            var rotated = await _sessions.TryRotateAsync(
                session.Id,
                session.RefreshTokenHash,
                RefreshTokenHasher.Hash(string.Empty, secret),
                utcNow.Add(JwtOptions.RefreshTokenLifetime),
                utcNow,
                cancellationToken);
            if (!rotated)
            {
                await AuditFailureAsync(
                    AuditEvents.Auth.TokenRefreshFailed,
                    "concurrent_refresh",
                    AuditOutcome.Suspicious,
                    session.UserId,
                    session.Id);
                throw Unauthorized();
            }

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

            var detached = 0;
            await _devices.ExecuteInTransactionAsync(async transactionToken =>
            {
                session.Revoke();
                await _sessions.SaveChangesAsync(transactionToken);

                // Logout policy: only the installation this session currently owns is released, and the update is
                // conditional on that. If the installation has since moved to another session or account (a delayed
                // logout), it matches nothing and the new association is left alone. The user's sessions on other
                // installations are untouched.
                detached = await _devices.DetachBySessionsAsync([session.Id], transactionToken);
            }, cancellationToken);

            await AuditAsync(AuditEvents.Auth.SessionRevoked, AuditOutcome.Success, session.UserId, session.Id);
            if (detached > 0)
            {
                await AuditAsync(AuditEvents.Device.LoggedOut, AuditOutcome.Success, session.UserId, session.Id);
            }
        }

        public async Task RevokeAllAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var revoked = 0;
            await _devices.ExecuteInTransactionAsync(async transactionToken =>
            {
                var sessions = await _sessions.ListByUserIdAsync(userId, transactionToken);
                foreach (var session in sessions)
                {
                    session.Revoke();
                }

                await _sessions.SaveChangesAsync(transactionToken);
                revoked = sessions.Count;

                // Every session is gone, so none of the user's installations may stay associated with the account.
                await _devices.DetachByUserAsync(userId, transactionToken);
            }, cancellationToken);

            await _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    AuditActorType.User,
                    userId,
                    AuditEvents.Auth.AllSessionsRevoked,
                    AuditOutcome.Success,
                    Metadata: new Dictionary<string, string?> { ["sessionCount"] = revoked.ToString() }),
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
