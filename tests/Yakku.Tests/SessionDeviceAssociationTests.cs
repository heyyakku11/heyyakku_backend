using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Devices;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Services;
using Yakku.Application.Devices.Validators;
using Yakku.Application.Guests.Services;
using Yakku.Application.Notifications.Services;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.PushNotifications.Services;
using Yakku.Application.PushNotifications.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>
/// User, device installation and login session relationships: multiple devices and sessions per user, one active
/// account per installation, device-specific logout, account switching and push isolation. Needs Docker.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SessionDeviceAssociationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public SessionDeviceAssociationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- multiple devices and sessions -------------------------------------------------------------------------

    [SkippableFact]
    public async Task OneUser_CanBeSignedInOnSeveralDevices_EachWithItsOwnSession()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var phone = NewInstallation();
        var tablet = NewInstallation();

        var phoneLogin = await NewSessionService(context).CreateAsync(user.Id, phone);
        var tabletLogin = await NewSessionService(context).CreateAsync(user.Id, tablet);

        await using var verify = _fixture.CreateContext();
        var phoneRow = await verify.Devices.SingleAsync(d => d.InstallationId == phone.InstallationId);
        var tabletRow = await verify.Devices.SingleAsync(d => d.InstallationId == tablet.InstallationId);
        Assert.Equal(SessionIdOf(phoneLogin.RefreshToken), phoneRow.CurrentSessionId);
        Assert.Equal(SessionIdOf(tabletLogin.RefreshToken), tabletRow.CurrentSessionId);
        Assert.Equal(user.Id, phoneRow.UserId);
        Assert.Equal(user.Id, tabletRow.UserId);

        var phoneSession = await verify.UserSessions.SingleAsync(s => s.Id == phoneRow.CurrentSessionId);
        Assert.Equal(phoneRow.Id, phoneSession.DeviceId);
        Assert.Equal(user.Id, phoneSession.UserId);

        var targets = await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(
            new[] { phoneRow.Id, tabletRow.Id }.Order(),
            targets.Select(d => d.Id).Order());
    }

    [SkippableFact]
    public async Task Logout_RevokesOnlyThatDevicesSession_AndLeavesTheOtherDeviceReceivingPush()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var phone = NewInstallation();
        var tablet = NewInstallation();
        var sessions = NewSessionService(context);
        var phoneLogin = await sessions.CreateAsync(user.Id, phone);
        var tabletLogin = await sessions.CreateAsync(user.Id, tablet);

        await sessions.RevokeAsync(phoneLogin.RefreshToken);

        await using var verify = _fixture.CreateContext();
        var phoneRow = await verify.Devices.SingleAsync(d => d.InstallationId == phone.InstallationId);
        var tabletRow = await verify.Devices.SingleAsync(d => d.InstallationId == tablet.InstallationId);
        Assert.Null(phoneRow.UserId);
        Assert.Null(phoneRow.CurrentSessionId);
        Assert.Null(phoneRow.PushToken);
        Assert.Equal(user.Id, tabletRow.UserId);
        Assert.Equal(SessionIdOf(tabletLogin.RefreshToken), tabletRow.CurrentSessionId);
        Assert.NotNull((await verify.UserSessions.SingleAsync(s => s.Id == SessionIdOf(phoneLogin.RefreshToken))).RevokedAt);
        Assert.Null((await verify.UserSessions.SingleAsync(s => s.Id == SessionIdOf(tabletLogin.RefreshToken))).RevokedAt);

        var targets = await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow);
        Assert.Equal([tabletRow.Id], targets.Select(d => d.Id));
    }

    [SkippableFact]
    public async Task SessionCap_EvictingASession_AlsoReleasesItsInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var oldest = NewInstallation();
        var sessions = NewSessionService(context);
        await sessions.CreateAsync(user.Id, oldest);
        for (var i = 0; i < 5; i++)
        {
            await sessions.CreateAsync(user.Id, NewInstallation());
        }

        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == oldest.InstallationId);
        Assert.Null(row.UserId);
        Assert.Null(row.CurrentSessionId);
        Assert.Equal(5, await verify.UserSessions.CountAsync(s => s.UserId == user.Id && s.RevokedAt == null));
        var targets = await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow);
        Assert.Equal(5, targets.Count);
    }

    // ---- account switch ----------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task AccountSwitch_MovesTheAssociation_ButNeverRewritesTheOldSessionsHistory()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var installation = NewInstallation();
        var sessions = NewSessionService(context);
        var aliceLogin = await sessions.CreateAsync(alice.Id, installation);
        var aliceSessionId = SessionIdOf(aliceLogin.RefreshToken);

        var bobLogin = await sessions.CreateAsync(bob.Id, installation);
        var bobSessionId = SessionIdOf(bobLogin.RefreshToken);

        await using var verify = _fixture.CreateContext();
        var device = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
        Assert.Equal(bob.Id, device.UserId);
        Assert.Equal(bobSessionId, device.CurrentSessionId);
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.InstallationId == installation.InstallationId));

        var aliceSession = await verify.UserSessions.SingleAsync(s => s.Id == aliceSessionId);
        Assert.Equal(alice.Id, aliceSession.UserId); // history: the session still belongs to Alice
        Assert.Equal(device.Id, aliceSession.DeviceId); // ... and to this installation
        Assert.NotNull(aliceSession.RevokedAt); // the displaced session is over
        var bobSession = await verify.UserSessions.SingleAsync(s => s.Id == bobSessionId);
        Assert.Equal(bob.Id, bobSession.UserId);
        Assert.Equal(device.Id, bobSession.DeviceId);
    }

    [SkippableFact]
    public async Task AfterAccountSwitch_PushForThePreviousAccount_NeverReachesTheInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var installation = NewInstallation();
        var sessions = NewSessionService(context);
        await sessions.CreateAsync(alice.Id, installation);
        var firebase = new RecordingFirebase();
        var push = NewPushService(context, firebase);

        // A list built while the installation still belonged to Alice, kept around (a queued job, a cache).
        await using var cacheContext = _fixture.CreateContext();
        var staleForAlice = await new DeviceRepository(cacheContext).GetPushTargetsByUserIdAsync(alice.Id, DateTime.UtcNow);
        Assert.Single(staleForAlice);

        await sessions.CreateAsync(bob.Id, installation);

        var toAlice = await Assert.ThrowsAsync<AppException>(() => push.SendToUserAsync(alice.Id, Payload()));
        Assert.True(toAlice.StatusCode is 400 or 404); // no device is associated with Alice any more
        var viaStaleList = await push.SendToDevicesAsync(staleForAlice, Payload());
        Assert.Equal(0, viaStaleList.EligibleDevices);
        await Assert.ThrowsAsync<AppException>(() => push.SendToDeviceAsync(staleForAlice[0], Payload()));
        Assert.Empty(firebase.SentTokens);

        var toBob = await push.SendToUserAsync(bob.Id, Payload());
        Assert.Equal(1, toBob.Successful);
        Assert.Equal(installation.PushToken, Assert.Single(firebase.SentTokens));
    }

    [SkippableFact]
    public async Task ADelayedLogout_CannotClearAnAssociationThatWasReassigned()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var installation = NewInstallation();
        var sessions = NewSessionService(context);
        var aliceLogin = await sessions.CreateAsync(alice.Id, installation);
        await sessions.CreateAsync(bob.Id, installation);

        // Alice's logout (or a cleanup) arrives after Bob took the installation over.
        var detached = await new DeviceRepository(context).DetachBySessionsAsync([SessionIdOf(aliceLogin.RefreshToken)]);

        Assert.Equal(0, detached);
        await using var verify = _fixture.CreateContext();
        var device = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
        Assert.Equal(bob.Id, device.UserId);
        Assert.NotNull(device.CurrentSessionId);
        Assert.True(device.IsActive);
        Assert.NotNull(device.PushToken);

        // Her real refresh token was revoked by the switch, so a late /logout call is simply rejected.
        var late = await Assert.ThrowsAsync<AppException>(() => sessions.RevokeAsync(aliceLogin.RefreshToken));
        Assert.Equal(401, late.StatusCode);
        await using var after = _fixture.CreateContext();
        Assert.Equal(bob.Id, (await after.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId)).UserId);
    }

    [SkippableFact]
    public async Task TheDatabase_RefusesADeviceWhoseUserIsNotTheUserOfItsCurrentSession()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var installation = NewInstallation();
        await NewSessionService(context).CreateAsync(alice.Id, installation);

        var violation = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE devices SET "UserId" = {bob.Id} WHERE "InstallationId" = {installation.InstallationId}"""));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, violation.SqlState);
    }

    // ---- push tokens -------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task FcmTokenRefresh_ReplacesTheTokenOnTheCurrentInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var installation = NewInstallation();
        var login = await NewSessionService(context).CreateAsync(user.Id, installation);
        var caller = DeviceCaller.ForUser(user.Id, SessionIdOf(login.RefreshToken));
        var refreshed = "refreshed-" + Guid.NewGuid().ToString("N");

        var response = await NewDeviceService(context).UpdateCurrentAsync(
            caller, installation.InstallationId, new UpdateDeviceRequest { PushToken = refreshed });

        Assert.True(response.PushEnabled);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
        Assert.Equal(refreshed, row.PushToken);
        Assert.Equal(Device.ComputeTokenHash(refreshed), row.PushTokenHash);
    }

    [SkippableFact]
    public async Task ASessionThatIsNoLongerCurrent_CannotUpdateTheInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var installation = NewInstallation();
        var sessions = NewSessionService(context);
        var aliceLogin = await sessions.CreateAsync(alice.Id, installation);
        await sessions.CreateAsync(bob.Id, installation);

        var exception = await Assert.ThrowsAsync<AppException>(() => NewDeviceService(context).UpdateCurrentAsync(
            DeviceCaller.ForUser(alice.Id, SessionIdOf(aliceLogin.RefreshToken)),
            installation.InstallationId,
            new UpdateDeviceRequest { PushToken = "hijack-" + Guid.NewGuid().ToString("N") }));

        Assert.True(exception.StatusCode is 401 or 404);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(
            installation.PushToken,
            (await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId)).PushToken);
    }

    [SkippableFact]
    public async Task ATokenHeldByAnotherAccountsLiveDevice_IsRefused_ButAStaleHolderIsReleased()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var aliceInstallation = NewInstallation();
        var sessions = NewSessionService(context);
        await sessions.CreateAsync(alice.Id, aliceInstallation);

        var stolen = NewInstallation();
        stolen.PushToken = aliceInstallation.PushToken;
        await sessions.CreateAsync(bob.Id, stolen); // login still succeeds, but the live holder keeps its token

        await using (var verify = _fixture.CreateContext())
        {
            Assert.Equal(
                aliceInstallation.PushToken,
                (await verify.Devices.SingleAsync(d => d.InstallationId == aliceInstallation.InstallationId)).PushToken);
            var bobSession = await verify.UserSessions.SingleAsync(s => s.UserId == bob.Id);
            Assert.Null(bobSession.DeviceId); // bound failed softly: the session exists, unbound
        }

        // Alice's session ends (expires): her installation is a stale holder and the token can move.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE user_sessions SET "ExpiresAt" = now() - interval '1 minute' WHERE "UserId" = {alice.Id}""");
        var bobLogin = await sessions.CreateAsync(bob.Id, stolen);

        await using var final = _fixture.CreateContext();
        Assert.Null((await final.Devices.SingleAsync(d => d.InstallationId == aliceInstallation.InstallationId)).PushToken);
        Assert.Equal(
            stolen.PushToken,
            (await final.Devices.SingleAsync(d => d.InstallationId == stolen.InstallationId)).PushToken);
    }

    [SkippableFact]
    public async Task AnInvalidTokenIsClearedOnlyWhileItIsStillTheCurrentOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var installation = NewInstallation();
        var login = await NewSessionService(context).CreateAsync(user.Id, installation);
        var caller = DeviceCaller.ForUser(user.Id, SessionIdOf(login.RefreshToken));
        var oldHash = Device.ComputeTokenHash(installation.PushToken!);
        var refreshed = "fresh-" + Guid.NewGuid().ToString("N");
        await NewDeviceService(context).UpdateCurrentAsync(
            caller, installation.InstallationId, new UpdateDeviceRequest { PushToken = refreshed });
        var deviceId = (await context.Devices.AsNoTracking()
            .SingleAsync(d => d.InstallationId == installation.InstallationId)).Id;
        var repository = new DeviceRepository(context);

        // FCM rejected the old token after the app already refreshed it: the new one must survive.
        Assert.False(await repository.ClearPushTokenIfMatchesAsync(deviceId, oldHash));
        Assert.True(await repository.ClearPushTokenIfMatchesAsync(deviceId, Device.ComputeTokenHash(refreshed)));

        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.Id == deviceId);
        Assert.Null(row.PushToken);
        Assert.Equal(user.Id, row.UserId); // the account association is untouched; the app can register a new token
        Assert.True(row.IsActive);
    }

    // ---- expired, revoked and stale sessions -------------------------------------------------------------------

    [SkippableFact]
    public async Task ExpiredAndRevokedSessions_StopPush_AndTheCleanupReleasesTheirInstallations()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var live = NewInstallation();
        var expired = NewInstallation();
        var revoked = NewInstallation();
        var sessions = NewSessionService(context);
        await sessions.CreateAsync(user.Id, live);
        var expiredLogin = await sessions.CreateAsync(user.Id, expired);
        var revokedLogin = await sessions.CreateAsync(user.Id, revoked);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE user_sessions SET "ExpiresAt" = now() - interval '1 minute' WHERE "Id" = {SessionIdOf(expiredLogin.RefreshToken)}""");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE user_sessions SET "RevokedAt" = now() WHERE "Id" = {SessionIdOf(revokedLogin.RefreshToken)}""");

        await using (var before = _fixture.CreateContext())
        {
            var targets = await new DeviceRepository(before).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow);
            Assert.Equal(live.InstallationId, Assert.Single(targets).InstallationId);
        }

        var result = await new SessionCleanupService(
            new UserSessionRepository(context), new DeviceRepository(context)).CleanupAsync();

        Assert.Equal(2, result.DevicesDetached);
        await using var verify = _fixture.CreateContext();
        foreach (var released in new[] { expired, revoked })
        {
            var row = await verify.Devices.SingleAsync(d => d.InstallationId == released.InstallationId);
            Assert.Null(row.UserId);
            Assert.Null(row.CurrentSessionId);
            Assert.Null(row.PushToken);
        }

        Assert.NotNull((await verify.Devices.SingleAsync(d => d.InstallationId == live.InstallationId)).CurrentSessionId);
    }

    [SkippableFact]
    public async Task ARevokedSession_CannotRegisterAnInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var sessions = NewSessionService(context);
        var login = await sessions.CreateAsync(user.Id);
        await sessions.RevokeAsync(login.RefreshToken);

        var exception = await Assert.ThrowsAsync<AppException>(() => NewDeviceService(context).RegisterAsync(
            DeviceCaller.ForUser(user.Id, SessionIdOf(login.RefreshToken)), NewInstallation()));

        Assert.Equal(401, exception.StatusCode);
    }

    [SkippableFact]
    public async Task LogoutEverywhere_ReleasesEveryInstallationOfTheUser_AndNoOthers()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var sessions = NewSessionService(context);
        var phone = NewInstallation();
        var tablet = NewInstallation();
        var bobs = NewInstallation();
        await sessions.CreateAsync(alice.Id, phone);
        await sessions.CreateAsync(alice.Id, tablet);
        await sessions.CreateAsync(bob.Id, bobs);

        await sessions.RevokeAllAsync(alice.Id);

        await using var verify = _fixture.CreateContext();
        Assert.All(
            await verify.Devices.Where(d => d.InstallationId == phone.InstallationId || d.InstallationId == tablet.InstallationId).ToListAsync(),
            d =>
            {
                Assert.Null(d.UserId);
                Assert.Null(d.CurrentSessionId);
            });
        Assert.Equal(bob.Id, (await verify.Devices.SingleAsync(d => d.InstallationId == bobs.InstallationId)).UserId);
        Assert.Empty(await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(alice.Id, DateTime.UtcNow));
    }

    // ---- concurrency -------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task TwoAccountsLoggingInOnTheSameInstallationAtOnce_LeaveOneConsistentOwner()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var alice = await AddUserAsync(setup);
        var bob = await AddUserAsync(setup);
        var installation = NewInstallation(); // the installation does not exist yet: both race to create it

        await using var aliceContext = _fixture.CreateContext();
        await using var bobContext = _fixture.CreateContext();
        var results = await Task.WhenAll(
            NewSessionService(aliceContext).CreateAsync(alice.Id, installation),
            NewSessionService(bobContext).CreateAsync(bob.Id, installation));

        await using var verify = _fixture.CreateContext();
        var device = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
        Assert.NotNull(device.UserId);
        var current = await verify.UserSessions.SingleAsync(s => s.Id == device.CurrentSessionId);
        Assert.Equal(device.UserId, current.UserId);
        Assert.Null(current.RevokedAt);
        Assert.Equal(device.Id, current.DeviceId);
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.InstallationId == installation.InstallationId));

        // Exactly one of the two sessions can still be the live one on this installation.
        var sessionIds = results.Select(r => SessionIdOf(r.RefreshToken)).ToList();
        var live = await verify.UserSessions.CountAsync(s => sessionIds.Contains(s.Id) && s.RevokedAt == null);
        Assert.Equal(1, live);
        var targets = await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(device.UserId!.Value, DateTime.UtcNow);
        Assert.Single(targets);
        var otherUser = device.UserId == alice.Id ? bob.Id : alice.Id;
        Assert.Empty(await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(otherUser, DateTime.UtcNow));
    }

    [SkippableFact]
    public async Task ALogoutRacingAnotherAccountsLogin_NeverClearsTheNewAccountsAssociation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        for (var round = 0; round < 5; round++)
        {
            await using var setup = _fixture.CreateContext();
            var alice = await AddUserAsync(setup);
            var bob = await AddUserAsync(setup);
            var installation = NewInstallation();
            var aliceLogin = await NewSessionService(setup).CreateAsync(alice.Id, installation);

            await using var logoutContext = _fixture.CreateContext();
            await using var loginContext = _fixture.CreateContext();
            var logout = NewSessionService(logoutContext).RevokeAsync(aliceLogin.RefreshToken);
            var login = NewSessionService(loginContext).CreateAsync(bob.Id, installation);
            try
            {
                await Task.WhenAll(logout, login);
            }
            catch (AppException)
            {
                // The logout may lose to the switch and find its session already revoked (401): that is correct.
            }

            await using var verify = _fixture.CreateContext();
            var device = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
            Assert.Equal(bob.Id, device.UserId);
            Assert.NotNull(device.CurrentSessionId);
            Assert.True(device.IsActive);
            Assert.NotNull(device.PushToken);
            Assert.Empty(await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(alice.Id, DateTime.UtcNow));
            Assert.Single(await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(bob.Id, DateTime.UtcNow));
        }
    }

    [SkippableFact]
    public async Task ConcurrentRefreshesWithTheSameToken_AllowExactlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var user = await AddUserAsync(setup);
        var login = await NewSessionService(setup).CreateAsync(user.Id);

        await using var first = _fixture.CreateContext();
        await using var second = _fixture.CreateContext();
        var attempts = new[]
        {
            Attempt(() => NewSessionService(first).RefreshAsync(login.RefreshToken)),
            Attempt(() => NewSessionService(second).RefreshAsync(login.RefreshToken))
        };
        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(ok => ok));
    }

    [SkippableFact]
    public async Task ConcurrentLoginsOfOneUser_NeverExceedTheSessionCap()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var user = await AddUserAsync(setup);
        var contexts = Enumerable.Range(0, 8).Select(_ => _fixture.CreateContext()).ToList();
        try
        {
            await Task.WhenAll(contexts.Select(context => NewSessionService(context).CreateAsync(user.Id, NewInstallation())));
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }

        await using var verify = _fixture.CreateContext();
        Assert.Equal(5, await verify.UserSessions.CountAsync(s => s.UserId == user.Id && s.RevokedAt == null));
        Assert.Equal(5, (await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow)).Count);
    }

    // ---- notification isolation and privacy --------------------------------------------------------------------

    [SkippableFact]
    public async Task PushIsOff_WhenTheAccountDisablesIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await NewSessionService(context).CreateAsync(user.Id, NewInstallation());
        var profile = await context.UserProfiles.SingleAsync(p => p.UserId == user.Id);
        profile.SetPushAllowed(false);
        await context.SaveChangesAsync();

        await using var verify = _fixture.CreateContext();
        Assert.Empty(await new DeviceRepository(verify).GetPushTargetsByUserIdAsync(user.Id, DateTime.UtcNow));
    }

    [SkippableFact]
    public async Task ANotificationCanBeOpenedOnlyByItsOwner_AndItsPushPayloadCarriesNoContent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var notification = new Notification(
            owner.Id,
            NotificationType.Normal,
            NotificationEventType.PollAnswered,
            "Sam answered your poll",
            "Sam said: pizza",
            null,
            null);
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        var service = new NotificationService(new NotificationRepository(context));

        var opened = await service.GetAsync(owner.Id, notification.Id);
        var denied = await Assert.ThrowsAsync<AppException>(() => service.GetAsync(other.Id, notification.Id));

        Assert.Equal(notification.Id, opened.Id);
        Assert.Equal(404, denied.StatusCode);

        var payload = PushPayloads.ForNotification(notification);
        Assert.DoesNotContain("Sam", payload.Title + payload.Body);
        Assert.DoesNotContain("pizza", payload.Title + payload.Body);
        Assert.Equal(["notificationId", "type"], payload.Data!.Keys.Order());
    }

    [SkippableFact]
    public async Task ALoginBindFailure_StillCreatesTheSession()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var guest = new Guest("hash-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow);
        context.Guests.Add(guest);
        await context.SaveChangesAsync();
        var installation = NewInstallation();
        await NewDeviceService(context).RegisterAsync(DeviceCaller.ForGuest(guest.Id), installation);

        // The OTP is already consumed by the time the device is bound: a guest-owned installation must not
        // lock the user out, and must not be taken over without the guest claim.
        var login = await NewSessionService(context).CreateAsync(user.Id, installation);

        await using var verify = _fixture.CreateContext();
        var session = await verify.UserSessions.SingleAsync(s => s.Id == SessionIdOf(login.RefreshToken));
        Assert.Null(session.DeviceId);
        var device = await verify.Devices.SingleAsync(d => d.InstallationId == installation.InstallationId);
        Assert.Equal(guest.Id, device.GuestId);
        Assert.Null(device.UserId);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    private static async Task<bool> Attempt(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (AppException)
        {
            return false;
        }
    }

    private static Guid SessionIdOf(string refreshToken) => Guid.Parse(refreshToken.Split('.')[0]);

    private static NotificationPayload Payload() => new() { Title = "Yakku", Body = "You have a new notification" };

    private static RegisterDeviceRequest NewInstallation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new RegisterDeviceRequest
        {
            InstallationId = "assoc-" + suffix,
            PushToken = "assoc-token-" + suffix,
            Platform = "android",
            DeviceModel = "Pixel 9",
            OsVersion = "15",
            AppVersion = "1.0.0",
            AppBuild = "100",
            Locale = "en-US",
            Timezone = "UTC",
            NotificationPermission = "granted"
        };
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"558{sequence:D10}{Random.Shared.Next(10, 99)}", $"Assoc {sequence}", $"assoc-anon-{sequence}-{Guid.NewGuid():N}"[..40]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static DeviceService NewDeviceService(YakkuDbContext context)
    {
        return new DeviceService(
            new DeviceRepository(context),
            new UserSessionRepository(context),
            new GuestRepository(context),
            new GuestTokenHasher(),
            new RegisterDeviceValidator(),
            new UpdateDeviceValidator(),
            new RecordingAuditLogWriter());
    }

    private static SessionService NewSessionService(YakkuDbContext context)
    {
        return new SessionService(
            new UserSessionRepository(context),
            new TokenService("assoc-tests-secret-assoc-tests-secret-1234567"),
            new RefreshTokenValidator(),
            new DeviceRepository(context),
            NewDeviceService(context),
            new RecordingAuditLogWriter());
    }

    private static PushNotificationService NewPushService(YakkuDbContext context, IFirebaseNotificationProvider firebase)
    {
        return new PushNotificationService(
            firebase,
            new DeviceRepository(context),
            new UserRepository(context),
            new NotificationPayloadValidator(),
            new TestDeviceNotificationRequestValidator(),
            new TestUserNotificationRequestValidator(),
            new RecordingAuditLogWriter());
    }

    private sealed class RecordingFirebase : IFirebaseNotificationProvider
    {
        public List<string> SentTokens { get; } = [];

        public Task<FirebaseTokenSendResult> SendAsync(
            string pushToken,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            SentTokens.Add(pushToken);
            return Task.FromResult(Accepted(pushToken));
        }

        public Task<IReadOnlyList<FirebaseTokenSendResult>> SendToManyAsync(
            IReadOnlyList<string> pushTokens,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            SentTokens.AddRange(pushTokens);
            return Task.FromResult<IReadOnlyList<FirebaseTokenSendResult>>(pushTokens.Select(Accepted).ToList());
        }

        private static FirebaseTokenSendResult Accepted(string token) =>
            new() { TokenFingerprint = token[..Math.Min(8, token.Length)], Success = true, MessageId = "msg-" + token };
    }
}
