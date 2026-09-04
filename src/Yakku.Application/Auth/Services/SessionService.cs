using FluentValidation;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Auth.Services
{
    public class SessionService : ISessionService
    {
        private readonly IUserSessionRepository _sessions;
        private readonly ITokenService _tokenService;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;

        public SessionService(
            IUserSessionRepository sessions,
            ITokenService tokenService,
            ISystemLogWriter systemLogWriter,
            IValidator<RefreshTokenRequest> refreshTokenValidator)
        {
            _sessions = sessions;
            _tokenService = tokenService;
            _systemLogWriter = systemLogWriter;
            _refreshTokenValidator = refreshTokenValidator;
        }

        public async Task<TokenResponse> CreateAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var secret = RefreshTokenHasher.GenerateSecret();
            var salt = RefreshTokenHasher.GenerateSalt();
            var session = new UserSession(
                userId,
                RefreshTokenHasher.Hash(salt, secret),
                salt,
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

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
            var salt = RefreshTokenHasher.GenerateSalt();
            session.Rotate(
                RefreshTokenHasher.Hash(salt, secret),
                salt,
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.SaveChangesAsync(cancellationToken);

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.SessionRefreshed,
                    Message = "Session refreshed.",
                    UserId = session.UserId,
                    Details = new { sessionId = session.Id }
                },
                cancellationToken);

            return ToResponse(session.UserId, session.Id, secret);
        }

        public async Task RevokeAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(refreshToken, cancellationToken);
            var userId = session.UserId;
            var sessionId = session.Id;
            await _sessions.DeleteAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.SessionRevoked,
                    Message = "Session revoked.",
                    UserId = userId,
                    Details = new { sessionId }
                },
                cancellationToken);
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
            if (session is null || session.IsExpired(DateTime.UtcNow))
            {
                if (session is not null)
                {
                    await _sessions.DeleteAsync(session, cancellationToken);
                    await _sessions.SaveChangesAsync(cancellationToken);
                }

                throw Unauthorized();
            }

            if (!RefreshTokenHasher.Verify(session.TokenSalt, secret, session.TokenHash))
            {
                throw Unauthorized();
            }

            return session;
        }

        private TokenResponse ToResponse(Guid userId, Guid sessionId, string secret)
        {
            return new TokenResponse
            {
                AccessToken = _tokenService.CreateAccessToken(userId),
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
