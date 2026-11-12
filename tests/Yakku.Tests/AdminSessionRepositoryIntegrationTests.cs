using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminAuth.Security;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>The real SQL behind the session guarantees: conditional updates that two requests cannot both win.</summary>
[Collection(PostgresCollection.Name)]
public class AdminSessionRepositoryIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public AdminSessionRepositoryIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Rotation_OnlyOneOfManyConcurrentRequestsWins()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (sessionId, token) = await NewSessionAsync();
        var oldHash = AdminSessionToken.Hash(token);
        var now = DateTime.UtcNow;

        var wins = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(async () =>
        {
            await using var context = _fixture.CreateContext();
            return await new AdminSessionRepository(context).TryRotateAsync(
                sessionId, oldHash, AdminSessionToken.Hash(AdminSessionToken.Generate()), now, now.AddSeconds(30), now.AddMinutes(30));
        })));

        Assert.Equal(1, wins.Count(won => won));
        await using var read = _fixture.CreateContext();
        var stored = await read.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Equal(oldHash, stored.PreviousSessionHash);
        Assert.NotEqual(oldHash, stored.SessionHash);
    }

    [SkippableFact]
    public async Task PreviousHash_IsAcceptedOnlyInsideTheGraceWindow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (sessionId, token) = await NewSessionAsync();
        var oldHash = AdminSessionToken.Hash(token);
        var now = DateTime.UtcNow;
        await using var context = _fixture.CreateContext();
        var repository = new AdminSessionRepository(context);
        await repository.TryRotateAsync(sessionId, oldHash, AdminSessionToken.Hash("new-token-hash-source"), now, now.AddSeconds(30), now.AddMinutes(30));

        Assert.NotNull(await repository.FindByHashAsync(oldHash, now.AddSeconds(10)));
        Assert.Null(await repository.FindByHashAsync(oldHash, now.AddSeconds(31)));
    }

    [SkippableFact]
    public async Task ARevokedSession_CannotBeRotatedOrTouchedBack()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (sessionId, token) = await NewSessionAsync();
        var hash = AdminSessionToken.Hash(token);
        var now = DateTime.UtcNow;
        await using var context = _fixture.CreateContext();
        var repository = new AdminSessionRepository(context);
        var adminId = await AdminIdOfAsync(sessionId);

        Assert.True(await repository.RevokeAsync(adminId, sessionId, "logout", now));
        Assert.False(await repository.RevokeAsync(adminId, sessionId, "again", now));
        Assert.False(await repository.TryRotateAsync(sessionId, hash, AdminSessionToken.Hash("x"), now, now, now.AddMinutes(30)));

        await repository.TouchAsync(sessionId, now.AddMinutes(5), now.AddMinutes(35));
        var stored = await _fixture.CreateContext().AdminSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Equal("logout", stored.RevocationReason);
        Assert.True(stored.LastActivityAt < now.AddMinutes(1));
    }

    [SkippableFact]
    public async Task RevokeAll_EndsLiveSessionsOfThatAdminOnly()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (a1, _) = await NewSessionAsync("a@example.test");
        var (a2, _) = await NewSessionAsync("a@example.test");
        var (b1, _) = await NewSessionAsync("b@example.test");
        await using var context = _fixture.CreateContext();
        var adminId = (await context.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == a1)).AdminId;

        var ended = await new AdminSessionRepository(context).RevokeAllAsync(adminId, "logout_all", DateTime.UtcNow);

        Assert.Equal(2, ended);
        await using var read = _fixture.CreateContext();
        Assert.NotNull((await read.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == a2)).RevokedAt);
        Assert.Null((await read.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == b1)).RevokedAt);
    }

    [SkippableFact]
    public async Task EndedSessions_AreCleanedUp_LiveOnesAreKept()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (revoked, _) = await NewSessionAsync();
        var (live, _) = await NewSessionAsync();
        await using var context = _fixture.CreateContext();
        var repository = new AdminSessionRepository(context);
        var revokedAdmin = await AdminIdOfAsync(revoked);
        await repository.RevokeAsync(revokedAdmin, revoked, "logout", DateTime.UtcNow.AddDays(-40));

        await repository.DeleteEndedBeforeAsync(DateTime.UtcNow.AddDays(-30));

        await using var read = _fixture.CreateContext();
        Assert.False(await read.AdminSessions.AnyAsync(s => s.Id == revoked));
        Assert.True(await read.AdminSessions.AnyAsync(s => s.Id == live));
    }

    private async Task<Guid> AdminIdOfAsync(Guid sessionId)
    {
        await using var context = _fixture.CreateContext();
        return (await context.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).AdminId;
    }

    [SkippableFact]
    public async Task AnAdmin_CannotRevokeAnotherAdminsSession()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (sessionId, _) = await NewSessionAsync();
        await using var context = _fixture.CreateContext();

        var revoked = await new AdminSessionRepository(context).RevokeAsync(Guid.NewGuid(), sessionId, "logout", DateTime.UtcNow);

        Assert.False(revoked);
        await using var read = _fixture.CreateContext();
        Assert.Null((await read.AdminSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId)).RevokedAt);
    }

    private async Task<(Guid SessionId, string Token)> NewSessionAsync(string? email = null)
    {
        await using var context = _fixture.CreateContext();
        email ??= $"admin-{Guid.NewGuid():N}@example.test";
        var admin = await context.Admins.FirstOrDefaultAsync(a => a.Email == email);
        if (admin is null)
        {
            admin = new Admin(email, "hash", AdminRole.SuperAdmin);
            admin.MarkVerified();
            context.Admins.Add(admin);
            await context.SaveChangesAsync();
        }

        var token = AdminSessionToken.Generate();
        var now = DateTime.UtcNow;
        var session = new AdminSession(admin.Id, AdminSessionToken.Hash(token), now, now.AddMinutes(30), now.AddHours(12));
        await new AdminSessionRepository(context).AddAsync(session);
        return (session.Id, token);
    }
}
