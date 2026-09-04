using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Domain.Entities;
using Xunit;

namespace Yakku.Application.Tests.Auth;

public class SessionServiceTests
{
    private const string JwtSecret = "test-jwt-secret-key-32-characters!";

    [Fact]
    public async Task Create_ReturnsAccessAndRefreshTokens()
    {
        var fixture = SessionFixture.Create();
        var userId = Guid.NewGuid();

        var result = await fixture.Service.CreateAsync(userId);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(900, result.AccessTokenExpiresInSeconds);
        Assert.Equal(604800, result.RefreshTokenExpiresInSeconds);
        Assert.Single(fixture.Sessions.Items);
        Assert.DoesNotContain(fixture.Sessions.Items[0].TokenHash, result.RefreshToken);
        Assert.True(RefreshTokenHasher.TryParse(result.RefreshToken, out var sessionId, out var secret));
        Assert.Equal(fixture.Sessions.Items[0].Id, sessionId);
        Assert.True(RefreshTokenHasher.Verify(
            fixture.Sessions.Items[0].TokenSalt,
            secret,
            fixture.Sessions.Items[0].TokenHash));
    }

    [Fact]
    public async Task Refresh_RotatesSecretAndRejectsOldToken()
    {
        var fixture = SessionFixture.Create();
        var created = await fixture.Service.CreateAsync(Guid.NewGuid());

        var refreshed = await fixture.Service.RefreshAsync(created.RefreshToken);

        Assert.NotEqual(created.RefreshToken, refreshed.RefreshToken);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.AccessToken));
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.SessionRefreshed);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RefreshAsync(created.RefreshToken));

        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public async Task Refresh_ExpiredSession_ReturnsUnauthorized()
    {
        var fixture = SessionFixture.Create();
        var userId = Guid.NewGuid();
        var secret = RefreshTokenHasher.GenerateSecret();
        var salt = RefreshTokenHasher.GenerateSalt();
        var session = new UserSession(
            userId,
            RefreshTokenHasher.Hash(salt, secret),
            salt,
            DateTime.UtcNow.AddMinutes(-1));
        fixture.Sessions.Items.Add(session);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RefreshAsync($"{session.Id}.{secret}"));

        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Empty(fixture.Sessions.Items);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsUnauthorized()
    {
        var fixture = SessionFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RefreshAsync("not-a-session-token"));

        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
    }

    [Fact]
    public async Task Revoke_ThenRefresh_ReturnsUnauthorized()
    {
        var fixture = SessionFixture.Create();
        var created = await fixture.Service.CreateAsync(Guid.NewGuid());

        await fixture.Service.RevokeAsync(created.RefreshToken);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.SessionRevoked);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RefreshAsync(created.RefreshToken));

        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Empty(fixture.Sessions.Items);
    }

    private sealed class SessionFixture
    {
        public FakeUserSessionRepository Sessions { get; }
        public FakeSystemLogWriter Logs { get; }
        public SessionService Service { get; }

        private SessionFixture(
            FakeUserSessionRepository sessions,
            FakeSystemLogWriter logs,
            SessionService service)
        {
            Sessions = sessions;
            Logs = logs;
            Service = service;
        }

        public static SessionFixture Create()
        {
            var sessions = new FakeUserSessionRepository();
            var logs = new FakeSystemLogWriter();
            var service = new SessionService(
                sessions,
                new TokenService(JwtSecret),
                logs,
                new RefreshTokenValidator());

            return new SessionFixture(sessions, logs, service);
        }
    }

    private sealed class FakeUserSessionRepository : IUserSessionRepository
    {
        public List<UserSession> Items { get; } = [];

        public Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            Items.Add(session);
            return Task.CompletedTask;
        }

        public Task<UserSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(session => session.Id == id));
        }

        public Task DeleteAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            Items.Remove(session);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
