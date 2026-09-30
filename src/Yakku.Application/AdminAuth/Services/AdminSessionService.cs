using FluentValidation;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminAuth.Services
{
    public class AdminSessionService : IAdminSessionService
    {
        private readonly IAdminSessionRepository _sessions;
        private readonly IAdminRepository _admins;
        private readonly ITokenService _tokenService;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<RefreshTokenRequest> _refreshTokenValidator;

        public AdminSessionService(
            IAdminSessionRepository sessions,
            IAdminRepository admins,
            ITokenService tokenService,
            ISystemLogWriter systemLogWriter,
            IValidator<RefreshTokenRequest> refreshTokenValidator)
        {
            _sessions = sessions;
            _admins = admins;
            _tokenService = tokenService;
            _systemLogWriter = systemLogWriter;
            _refreshTokenValidator = refreshTokenValidator;
        }

        public async Task<TokenResponse> CreateAsync(
            Guid adminId,
            string email,
            CancellationToken cancellationToken = default)
        {
            var secret = RefreshTokenHasher.GenerateSecret();
            var session = new AdminSession(
                adminId,
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.AddAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);

            return ToResponse(adminId, email, session.Id, secret);
        }

        public async Task<TokenResponse> RefreshAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(refreshToken, cancellationToken);
            var admin = await _admins.GetByIdAsync(session.AdminId, cancellationToken);
            if (admin is null || admin.Status == AdminStatus.Disabled)
            {
                throw Unauthorized();
            }

            var secret = RefreshTokenHasher.GenerateSecret();
            session.Rotate(
                RefreshTokenHasher.Hash(string.Empty, secret),
                DateTime.UtcNow.Add(JwtOptions.RefreshTokenLifetime));

            await _sessions.SaveChangesAsync(cancellationToken);

            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.AdminSessionRefreshed,
                    Message = "Admin session refreshed.",
                    Details = new { sessionId = session.Id, adminId = admin.Id }
                },
                cancellationToken);

            return ToResponse(admin.Id, admin.Email, session.Id, secret);
        }

        public async Task RevokeAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            var session = await LoadValidSessionAsync(refreshToken, cancellationToken);
            var adminId = session.AdminId;
            var sessionId = session.Id;
            await _sessions.DeleteAsync(session, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.AdminSessionRevoked,
                    Message = "Admin session revoked.",
                    Details = new { sessionId, adminId }
                },
                cancellationToken);
        }

        public async Task RevokeAllAsync(
            Guid adminId,
            CancellationToken cancellationToken = default)
        {
            await _sessions.DeleteAllByAdminIdAsync(adminId, cancellationToken);
            await _sessions.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.AdminSessionRevoked,
                    Message = "All admin sessions revoked.",
                    Details = new { adminId }
                },
                cancellationToken);
        }

        private async Task<AdminSession> LoadValidSessionAsync(
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
                if (session is not null)
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
