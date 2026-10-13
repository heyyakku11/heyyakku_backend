using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Application.Auth.Services
{
    public class SessionService : ISessionService
    {
        private readonly IUserSessionRepository _sessions;
        private readonly ITokenService _tokenService;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;

        public SessionService(
            IUserSessionRepository sessions,
            ITokenService tokenService,
            IValidator<RefreshTokenRequest> refreshTokenValidator)
        {
            _sessions = sessions;
            _tokenService = tokenService;
            _refreshTokenValidator = refreshTokenValidator;
        }

        public async Task<TokenResponse> CreateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            const int maxLiveSessions = 5;
            var utcNow = DateTime.UtcNow;
            var liveSessions = await _sessions.ListLiveByUserIdAsync(userId, utcNow, cancellationToken);
            if (liveSessions.Count >= maxLiveSessions)
            {
                var leastActive = liveSessions
                    .OrderBy(session => session.LastActiveAt ?? DateTime.MinValue)
                    .ThenBy(session => session.CreatedAt)
                    .First();
                leastActive.Revoke();
            }

            var secret = RefreshTokenHasher.GenerateSecret();
            var session = new UserSession(
                userId,
                RefreshTokenHasher.Hash(string.Empty, secret),
                utcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);

            return ToResponse(userId, session.Id, secret);
        }

        public async Task<TokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(refreshToken, cancellationToken);

            var secret = RefreshTokenHasher.GenerateSecret();
            session.Rotate(
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.SaveChangesAsync(cancellationToken);

            return ToResponse(session.UserId, session.Id, secret);
        }

        public async Task RevokeAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(refreshToken, cancellationToken);
            session.Revoke();
            await _sessions.SaveChangesAsync(cancellationToken);
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
        }

        private async Task<UserSession> LoadValidSessionAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            var request = new RefreshTokenRequest { RefreshToken = refreshToken };
            await _refreshTokenValidator.ValidateAndThrowAsync(request, cancellationToken);

            if (!RefreshTokenHasher.TryParse(refreshToken.Trim(), out var sessionId, out var secret))
            {
                throw Unauthorized();
            }

            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is null || session.RevokedAt is not null || session.IsExpired(DateTime.UtcNow))
            {
                if (session is not null && session.RevokedAt is null && session.IsExpired(DateTime.UtcNow))
                {
                    await _sessions.DeleteAsync(session, cancellationToken);
                    await _sessions.SaveChangesAsync(cancellationToken);
                }

                throw Unauthorized();
            }

            if (!RefreshTokenHasher.Verify(string.Empty, secret, session.RefreshTokenHash))
            {
                throw Unauthorized();
            }

            return session;
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
