using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.Audit;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class AdminSessionServiceTests
{
    private static readonly AdminClientInfo Client = new("203.0.113.9", "xunit");

    [Fact]
    public async Task Create_StoresOnlyTheHash_AndEveryLoginGetsAnUnrelatedToken()
    {
        var h = new Harness();

        var first = await h.Service.CreateAsync(h.Admin.Id, Client);
        var second = await h.Service.CreateAsync(h.Admin.Id, Client);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.Equal(AdminSessionToken.EncodedLength, first.Token.Length);
        Assert.All(h.Sessions.All, s =>
        {
            Assert.NotEqual(first.Token, s.SessionHash);
            Assert.NotEqual(second.Token, s.SessionHash);
            Assert.Equal(64, s.SessionHash.Length);
        });
        // Several devices can be signed in at once.
        Assert.Equal(2, h.Sessions.All.Count);
    }

    [Fact]
    public async Task Authenticate_ValidToken_ReturnsTheAdminFromTheDatabase()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);

        var result = await h.Service.AuthenticateAsync(issue.Token, Client);

        Assert.True(result.Succeeded);
        Assert.Equal(h.Admin.Id, result.AdminId);
        Assert.Equal(issue.SessionId, result.SessionId);
        Assert.Equal(h.Admin.Email, result.Email);
        Assert.Equal(AdminRole.SuperAdmin, result.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("this is not base64url but it is exactly 43 chars!!")]
    public async Task Authenticate_MissingOrMalformedToken_IsRejectedWithoutTouchingTheDatabase(string? token)
    {
        var h = new Harness();
        await h.Service.CreateAsync(h.Admin.Id, Client);

        var result = await h.Service.AuthenticateAsync(token, Client);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Authenticate_UnknownToken_IsRejected_AndRecorded()
    {
        var h = new Harness();

        var result = await h.Service.AuthenticateAsync(AdminSessionToken.Generate(), Client);

        Assert.False(result.Succeeded);
        Assert.Equal("session_not_found", result.FailureReason);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.SessionRejected);
    }

    [Fact]
    public async Task Authenticate_IdleTimeout_EndsTheSession_ForGood()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);

        h.Time.Advance(h.Options.IdleTimeout + TimeSpan.FromSeconds(1));
        var late = await h.Service.AuthenticateAsync(issue.Token, Client);

        Assert.False(late.Succeeded);
        Assert.Equal(AdminSessionRevocationReasons.IdleTimeout, late.FailureReason);

        // Even if the clock were wrong later, an ended session stays ended.
        h.Time.Advance(-h.Options.IdleTimeout);
        Assert.False((await h.Service.AuthenticateAsync(issue.Token, Client)).Succeeded);
    }

    [Fact]
    public async Task Authenticate_ActivityKeepsTheSessionAlive_ButNeverPastTheAbsoluteLimit()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);
        var token = issue.Token;

        // Active every 20 minutes (inside the 30 minute idle timeout) for almost the whole absolute lifetime.
        while (h.Time.GetUtcNow() - new DateTimeOffset(issue.AbsoluteExpiresAt - h.Options.AbsoluteTimeout, TimeSpan.Zero)
               < h.Options.AbsoluteTimeout - TimeSpan.FromMinutes(25))
        {
            h.Time.Advance(TimeSpan.FromMinutes(20));
            var result = await h.Service.AuthenticateAsync(token, Client);
            Assert.True(result.Succeeded);
            token = result.RotatedToken ?? token;
        }

        h.Time.Advance(TimeSpan.FromMinutes(30));
        var final = await h.Service.AuthenticateAsync(token, Client);

        Assert.False(final.Succeeded);
        Assert.Equal(AdminSessionRevocationReasons.AbsoluteTimeout, final.FailureReason);
    }

    [Fact]
    public async Task Authenticate_ARevokedSession_NeverWorksAgain_AndIsFlaggedSuspicious()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);
        await h.Service.RevokeAsync(h.Admin.Id, issue.SessionId, AdminSessionRevocationReasons.Logout);

        var result = await h.Service.AuthenticateAsync(issue.Token, Client);

        Assert.False(result.Succeeded);
        Assert.Equal("session_revoked", result.FailureReason);
        Assert.Contains(h.Audit.Authentication, e =>
            e.EventType == AuditEvents.Auth.SessionRejected && e.Outcome == AuditOutcome.Suspicious);
    }

    [Fact]
    public async Task Authenticate_ADisabledAdmin_LosesEverySession_Immediately()
    {
        var h = new Harness();
        var phone = await h.Service.CreateAsync(h.Admin.Id, Client);
        var laptop = await h.Service.CreateAsync(h.Admin.Id, Client);

        AdminTestAdmins.Disable(h.Admin);
        var result = await h.Service.AuthenticateAsync(phone.Token, Client);

        Assert.False(result.Succeeded);
        Assert.Equal("admin_not_active", result.FailureReason);
        // The other device's session was revoked too, not just the one that was used.
        Assert.False((await h.Service.AuthenticateAsync(laptop.Token, Client)).Succeeded);
        Assert.All(h.Sessions.All, s => Assert.NotNull(s.RevokedAt));
    }

    [Fact]
    public async Task Rotation_IssuesANewToken_TheOldOneSurvivesOnlyTheGraceWindow()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);

        h.Time.Advance(h.Options.RotationInterval + TimeSpan.FromSeconds(1));
        var rotated = await h.Service.AuthenticateAsync(issue.Token, Client);

        Assert.True(rotated.Succeeded);
        Assert.NotNull(rotated.RotatedToken);
        Assert.NotEqual(issue.Token, rotated.RotatedToken);

        // A request already in flight with the old token still works for a moment...
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Assert.True((await h.Service.AuthenticateAsync(issue.Token, Client)).Succeeded);
        Assert.True((await h.Service.AuthenticateAsync(rotated.RotatedToken, Client)).Succeeded);

        // ...but not once the grace window is over, so a copied old cookie is useless.
        h.Time.Advance(h.Options.RotationGrace);
        Assert.False((await h.Service.AuthenticateAsync(issue.Token, Client)).Succeeded);
        Assert.True((await h.Service.AuthenticateAsync(rotated.RotatedToken, Client)).Succeeded);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.SessionRotated);
    }

    [Fact]
    public async Task Rotation_UsingTheOldTokenInTheGraceWindow_DoesNotRotateAgain()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);
        h.Time.Advance(h.Options.RotationInterval + TimeSpan.FromSeconds(1));
        await h.Service.AuthenticateAsync(issue.Token, Client);

        var again = await h.Service.AuthenticateAsync(issue.Token, Client);

        Assert.True(again.Succeeded);
        Assert.Null(again.RotatedToken);
    }

    [Fact]
    public async Task Rotation_ManyRequestsAtOnce_RotateExactlyOnce_AndNobodyIsLoggedOut()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);
        h.Time.Advance(h.Options.RotationInterval + TimeSpan.FromSeconds(1));

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => h.Service.AuthenticateAsync(issue.Token, Client))));

        Assert.All(results, r => Assert.True(r.Succeeded));
        var rotatedTokens = results.Where(r => r.RotatedToken is not null).ToList();
        Assert.Single(rotatedTokens);
        Assert.True((await h.Service.AuthenticateAsync(rotatedTokens[0].RotatedToken, Client)).Succeeded);
    }

    [Fact]
    public async Task Logout_EndsOnlyThisSession()
    {
        var h = new Harness();
        var phone = await h.Service.CreateAsync(h.Admin.Id, Client);
        var laptop = await h.Service.CreateAsync(h.Admin.Id, Client);

        await h.Service.RevokeAsync(h.Admin.Id, phone.SessionId, AdminSessionRevocationReasons.Logout);

        Assert.False((await h.Service.AuthenticateAsync(phone.Token, Client)).Succeeded);
        Assert.True((await h.Service.AuthenticateAsync(laptop.Token, Client)).Succeeded);
    }

    [Fact]
    public async Task LogoutAll_EndsEverySession_OfThatAdminOnly()
    {
        var h = new Harness();
        var other = AdminTestAdmins.Active("other@example.com");
        h.Sessions.AddAdmin(other);
        var mine1 = await h.Service.CreateAsync(h.Admin.Id, Client);
        var mine2 = await h.Service.CreateAsync(h.Admin.Id, Client);
        var theirs = await h.Service.CreateAsync(other.Id, Client);

        await h.Service.RevokeAllAsync(h.Admin.Id, AdminSessionRevocationReasons.LogoutAll);

        Assert.False((await h.Service.AuthenticateAsync(mine1.Token, Client)).Succeeded);
        Assert.False((await h.Service.AuthenticateAsync(mine2.Token, Client)).Succeeded);
        Assert.True((await h.Service.AuthenticateAsync(theirs.Token, Client)).Succeeded);
        Assert.Contains(h.Audit.Authentication, e => e.EventType == AuditEvents.Auth.AllSessionsRevoked);
    }

    [Fact]
    public async Task Revoke_CannotEndAnotherAdminsSession_AndAnUnknownIdIsAHarmlessNoOp()
    {
        var h = new Harness();
        var other = AdminTestAdmins.Active("other.com");
        h.Sessions.AddAdmin(other);
        var mine = await h.Service.CreateAsync(h.Admin.Id, Client);
        var theirs = await h.Service.CreateAsync(other.Id, Client);

        await h.Service.RevokeAsync(h.Admin.Id, theirs.SessionId, AdminSessionRevocationReasons.Logout);
        await h.Service.RevokeAsync(h.Admin.Id, Guid.NewGuid(), AdminSessionRevocationReasons.Logout);

        Assert.True((await h.Service.AuthenticateAsync(theirs.Token, Client)).Succeeded);
        Assert.True((await h.Service.AuthenticateAsync(mine.Token, Client)).Succeeded);
    }

    [Fact]
    public async Task Audit_NeverContainsTheTokenOrItsHash()
    {
        var h = new Harness();
        var issue = await h.Service.CreateAsync(h.Admin.Id, Client);
        h.Time.Advance(h.Options.RotationInterval + TimeSpan.FromSeconds(1));
        var rotated = await h.Service.AuthenticateAsync(issue.Token, Client);
        await h.Service.AuthenticateAsync(AdminSessionToken.Generate(), Client);
        await h.Service.RevokeAllAsync(h.Admin.Id, AdminSessionRevocationReasons.LogoutAll);

        var log = string.Join("\n", h.Audit.Authentication.Select(e => e.ToString()));

        Assert.DoesNotContain(issue.Token, log);
        Assert.DoesNotContain(rotated.RotatedToken!, log);
        Assert.DoesNotContain(AdminSessionToken.Hash(issue.Token), log);
    }

    [Fact]
    public void CsrfTokens_AreBoundToTheirSession()
    {
        var csrf = new AdminCsrfTokens(AdminTestAdmins.Options());
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var token = csrf.Create(a);

        Assert.True(csrf.IsValid(a, token));
        Assert.False(csrf.IsValid(b, token));
        Assert.False(csrf.IsValid(a, token + "x"));
        Assert.False(csrf.IsValid(a, null));
        Assert.False(csrf.IsValid(a, ""));
        Assert.Equal(token, csrf.Create(a));
        Assert.NotEqual(token, csrf.Create(b));
    }

    [Fact]
    public void CsrfTokens_DifferentServerKeys_DoNotAcceptEachOther()
    {
        var other = AdminTestAdmins.Options();
        var sessionId = Guid.NewGuid();
        var forged = new AdminCsrfTokens(new AdminSessionOptions { CsrfKey = new byte[32].Select(_ => (byte)7).ToArray() })
            .Create(sessionId);

        Assert.False(new AdminCsrfTokens(other).IsValid(sessionId, forged));
    }

    private sealed class Harness
    {
        public Harness()
        {
            Admin = AdminTestAdmins.Active();
            Sessions.AddAdmin(Admin);
            Service = new AdminSessionService(Sessions, Options, Audit, Time);
        }

        public Admin Admin { get; }
        public AdminSessionOptions Options { get; } = AdminTestAdmins.Options();
        public ManualTimeProvider Time { get; } = new();
        public InMemoryAdminSessionRepository Sessions { get; } = new();
        public RecordingAuditLogWriter Audit { get; } = new();
        public AdminSessionService Service { get; }
    }
}
