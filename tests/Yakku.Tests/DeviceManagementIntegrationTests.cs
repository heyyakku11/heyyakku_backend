using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Services;
using Yakku.Application.Devices.Validators;
using Yakku.Application.Guests;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class DeviceManagementIntegrationTests
{
    private static readonly GuestTokenHasher Hasher = new();
    private readonly PostgresFixture _fixture;

    public DeviceManagementIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- register / upsert -------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task Guest_RegistersAnInstallation_AndRepeatingItUpdatesInsteadOfDuplicating()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var guest = await AddGuestAsync(context);
        var service = NewDeviceService(context);
        var request = NewRequest();

        var first = await service.RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);
        request.AppVersion = "2.0.0";
        var second = await service.RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);

        Assert.True(first.Created);
        Assert.False(second.Created);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(guest.Id, row.GuestId);
        Assert.Null(row.UserId);
        Assert.Equal("2.0.0", row.AppVersion);
    }

    [SkippableFact]
    public async Task User_RegistersWithTheirSession_AndTheResponseNeverContainsThePushToken()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var request = NewRequest();

        var result = await NewDeviceService(context).RegisterAsync(
            DeviceCaller.ForUser(user.Id, session.Id), request);

        Assert.True(result.Device.PushEnabled);
        Assert.DoesNotContain(request.PushToken!, JsonSerializer.Serialize(result));
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(user.Id, row.UserId);
        Assert.Null(row.GuestId);
        Assert.Equal(row.Id, (await verify.UserSessions.SingleAsync(s => s.Id == session.Id)).DeviceId);
        Assert.Equal(Device.ComputeTokenHash(request.PushToken!), row.PushTokenHash);
    }

    [SkippableFact]
    public async Task ConcurrentRegistrations_OfOneInstallation_ProduceExactlyOneRecord()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(setup);
        var request = NewRequest();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            return await NewDeviceService(context).RegisterAsync(
                DeviceCaller.ForUser(user.Id, session.Id),
                new RegisterDeviceRequest
                {
                    InstallationId = request.InstallationId,
                    PushToken = request.PushToken,
                    Platform = request.Platform,
                    NotificationPermission = request.NotificationPermission
                });
        }));

        Assert.Equal(1, results.Count(r => r.Created));
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.InstallationId == request.InstallationId));
    }

    [SkippableFact]
    public async Task ConcurrentRegistrations_OfDifferentInstallations_WithOneToken_LeaveOneHolder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(setup);
        var token = "shared-" + Guid.NewGuid().ToString("N");

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            var request = NewRequest();
            request.PushToken = token;
            try
            {
                await NewDeviceService(context).RegisterAsync(DeviceCaller.ForUser(user.Id, session.Id), request);
                return true;
            }
            catch (AppException)
            {
                return false;
            }
        }));

        // However the race resolves, the unique index guarantees a single holder and nothing blows up.
        Assert.Contains(true, outcomes);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.PushTokenHash == Device.ComputeTokenHash(token)));
    }

    // ---- token rotation / permission ---------------------------------------------------------------------------

    [SkippableFact]
    public async Task TokenRotation_UpdatesTheSameInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var request = NewRequest();
        await service.RegisterAsync(caller, request);
        var rotated = "rotated-" + Guid.NewGuid().ToString("N");

        var updated = await service.UpdateCurrentAsync(
            caller, request.InstallationId, new UpdateDeviceRequest { PushToken = rotated });

        Assert.True(updated.PushEnabled);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(rotated, row.PushToken);
        Assert.Equal(Device.ComputeTokenHash(rotated), row.PushTokenHash);
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.UserId == user.Id));
    }

    [SkippableFact]
    public async Task Update_CanTargetTheCurrentSessionDevice_WithoutAnInstallationHeader()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        await service.RegisterAsync(caller, NewRequest());

        var updated = await service.UpdateCurrentAsync(
            caller, null, new UpdateDeviceRequest { Locale = "fr-FR" });

        Assert.Equal("fr-FR", updated.Locale);
        Assert.True(updated.IsCurrent);
    }

    [SkippableFact]
    public async Task TokenMovingToAnotherInstallationOfTheSameOwner_ClearsTheStaleCopy()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var first = NewRequest();
        await service.RegisterAsync(caller, first);
        var second = NewRequest();
        second.PushToken = first.PushToken;
        var secondSession = await AddSessionAsync(context, user); // one session per installation

        await service.RegisterAsync(DeviceCaller.ForUser(user.Id, secondSession.Id), second);

        await using var verify = _fixture.CreateContext();
        Assert.Null((await verify.Devices.SingleAsync(d => d.InstallationId == first.InstallationId)).PushToken);
        Assert.Equal(first.PushToken, (await verify.Devices.SingleAsync(d => d.InstallationId == second.InstallationId)).PushToken);
    }

    [SkippableFact]
    public async Task PushToken_HeldByAnotherOwner_IsRefused_AndTheHolderKeepsIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (alice, aliceSession) = await AddUserWithSessionAsync(context);
        var (bob, bobSession) = await AddUserWithSessionAsync(context);
        var audit = new RecordingAuditLogWriter();
        var service = NewDeviceService(context, audit);
        var aliceRequest = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, aliceSession.Id), aliceRequest);
        var bobRequest = NewRequest();
        bobRequest.PushToken = aliceRequest.PushToken;

        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.RegisterAsync(DeviceCaller.ForUser(bob.Id, bobSession.Id), bobRequest));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.DeviceConflict, exception.ErrorCode);
        Assert.Equal("pushToken", exception.Field);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(aliceRequest.PushToken, (await verify.Devices.SingleAsync(d => d.InstallationId == aliceRequest.InstallationId)).PushToken);
        Assert.False(await verify.Devices.AnyAsync(d => d.InstallationId == bobRequest.InstallationId));
        var denied = Assert.Single(audit.Authentication, e => e.EventType == AuditEvents.Device.OwnershipConflict);
        Assert.Equal(AuditOutcome.Denied, denied.Outcome);
        Assert.DoesNotContain(aliceRequest.PushToken!, JsonSerializer.Serialize(audit.Authentication));
    }

    [SkippableFact]
    public async Task NotificationPermission_DeniedStopsPush_AndRetainsTheRecord()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var request = NewRequest();
        await service.RegisterAsync(caller, request);

        var denied = await service.UpdateCurrentAsync(
            caller, request.InstallationId, new UpdateDeviceRequest { NotificationPermission = "denied" });
        Assert.False(denied.PushEnabled);
        Assert.Equal("denied", denied.NotificationPermission);
        Assert.True(denied.IsActive);

        // While denied, a supplied token is ignored rather than stored.
        var stillDenied = await service.UpdateCurrentAsync(
            caller, request.InstallationId, new UpdateDeviceRequest { PushToken = "new-" + Guid.NewGuid().ToString("N") });
        Assert.False(stillDenied.PushEnabled);

        var granted = await service.UpdateCurrentAsync(
            caller,
            request.InstallationId,
            new UpdateDeviceRequest { NotificationPermission = "granted", PushToken = "granted-" + Guid.NewGuid().ToString("N") });
        Assert.True(granted.PushEnabled);

        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.InstallationId == request.InstallationId));
    }

    [SkippableFact]
    public async Task Update_WithNothingNew_IsASafeNoOp()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var audit = new RecordingAuditLogWriter();
        var service = NewDeviceService(context, audit);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var request = NewRequest();
        await service.RegisterAsync(caller, request);
        await using var before = _fixture.CreateContext();
        var stamp = (await before.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).UpdatedAt;

        await service.UpdateCurrentAsync(caller, request.InstallationId, new UpdateDeviceRequest());
        await service.UpdateCurrentAsync(
            caller, request.InstallationId, new UpdateDeviceRequest { Locale = request.Locale, PushToken = request.PushToken });

        await using var after = _fixture.CreateContext();
        Assert.Equal(stamp, (await after.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).UpdatedAt);
        Assert.DoesNotContain(audit.Authentication, e => e.EventType == AuditEvents.Device.Updated);
    }

    [SkippableFact]
    public async Task Update_OfAnUnregisteredDevice_RefusesANewToken()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var request = NewRequest();
        await service.RegisterAsync(caller, request);
        await service.UnregisterCurrentAsync(caller, request.InstallationId);

        var exception = await Assert.ThrowsAnyAsync<AppException>(() => service.UpdateCurrentAsync(
            caller, request.InstallationId, new UpdateDeviceRequest { PushToken = "x-" + Guid.NewGuid().ToString("N") }));

        Assert.Equal(409, exception.StatusCode);
    }

    // ---- cross-user access / missing devices / authentication --------------------------------------------------

    [SkippableFact]
    public async Task OtherUsers_CannotUpdateOrUnregisterSomeoneElsesDevice()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (alice, aliceSession) = await AddUserWithSessionAsync(context);
        var (bob, bobSession) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, aliceSession.Id), request);
        var bobCaller = DeviceCaller.ForUser(bob.Id, bobSession.Id);

        var update = await Assert.ThrowsAnyAsync<AppException>(() => service.UpdateCurrentAsync(
            bobCaller, request.InstallationId, new UpdateDeviceRequest { Locale = "xx-XX" }));
        var delete = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.UnregisterCurrentAsync(bobCaller, request.InstallationId));

        // The same 404 as a device that does not exist: ownership is not revealed.
        Assert.Equal(404, update.StatusCode);
        Assert.Equal(404, delete.StatusCode);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.True(row.IsActive);
        Assert.NotNull(row.PushToken);
        Assert.Equal(request.Locale, row.Locale);
    }

    [SkippableFact]
    public async Task InstallationOwnedByAnotherAccount_CannotBeTakenOver_WhileTheirSessionIsLive()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (alice, aliceSession) = await AddUserWithSessionAsync(context);
        var (bob, bobSession) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, aliceSession.Id), request);

        var takeover = NewRequest();
        takeover.InstallationId = request.InstallationId;
        var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.RegisterAsync(DeviceCaller.ForUser(bob.Id, bobSession.Id), takeover));

        Assert.Equal(409, exception.StatusCode);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(alice.Id, (await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).UserId);
    }

    [SkippableFact]
    public async Task MissingDevice_IsNotFound_AndGuestsMustNameTheInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var guest = await AddGuestAsync(context);
        var service = NewDeviceService(context);

        var missing = await Assert.ThrowsAnyAsync<AppException>(() => service.UpdateCurrentAsync(
            DeviceCaller.ForUser(user.Id, session.Id), "does-not-exist", new UpdateDeviceRequest()));
        var noSessionDevice = await Assert.ThrowsAnyAsync<AppException>(() => service.UnregisterCurrentAsync(
            DeviceCaller.ForUser(user.Id, session.Id), null));
        var guestWithoutHeader = await Assert.ThrowsAnyAsync<AppException>(() => service.UpdateCurrentAsync(
            DeviceCaller.ForGuest(guest.Id), null, new UpdateDeviceRequest()));

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(404, noSessionDevice.StatusCode);
        Assert.Equal(400, guestWithoutHeader.StatusCode);
    }

    [SkippableFact]
    public async Task RevokedOrExpiredSessions_AreRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var expired = new UserSession(user.Id, "h", DateTime.UtcNow.AddMinutes(-1));
        context.UserSessions.Add(expired);
        session.Revoke();
        await context.SaveChangesAsync();
        var service = NewDeviceService(context);

        foreach (var sessionId in new[] { session.Id, expired.Id, Guid.NewGuid() })
        {
            var exception = await Assert.ThrowsAnyAsync<AppException>(() =>
                service.RegisterAsync(DeviceCaller.ForUser(user.Id, sessionId), NewRequest()));
            Assert.Equal(401, exception.StatusCode);
        }

        // A session that belongs to someone else is not usable either.
        var (other, otherSession) = await AddUserWithSessionAsync(context);
        var crossed = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.RegisterAsync(DeviceCaller.ForUser(user.Id, otherSession.Id), NewRequest()));
        Assert.Equal(401, crossed.StatusCode);
    }

    [SkippableFact]
    public async Task List_ReturnsOnlyTheCallersDevices_WithoutTokens_AndMarksTheCurrentOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (alice, aliceSession) = await AddUserWithSessionAsync(context);
        var (bob, bobSession) = await AddUserWithSessionAsync(context);
        var service = NewDeviceService(context);
        var phone = NewRequest();
        var tablet = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, aliceSession.Id), phone);
        var aliceTabletSession = await AddSessionAsync(context, alice);
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, aliceTabletSession.Id), tablet);
        var bobRequest = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(bob.Id, bobSession.Id), bobRequest);

        var list = await service.ListAsync(DeviceCaller.ForUser(alice.Id, aliceSession.Id), phone.InstallationId);

        Assert.Equal(2, list.Count);
        Assert.DoesNotContain(list, d => d.InstallationId == bobRequest.InstallationId);
        Assert.Equal(phone.InstallationId, Assert.Single(list, d => d.IsCurrent).InstallationId);
        var json = JsonSerializer.Serialize(list);
        Assert.DoesNotContain(phone.PushToken!, json);
        Assert.DoesNotContain(tablet.PushToken!, json);
        Assert.DoesNotContain(bobRequest.PushToken!, json);
        Assert.All(list, d => Assert.True(d.PushEnabled));

        var guest = await AddGuestAsync(context);
        var guestList = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.ListAsync(DeviceCaller.ForGuest(guest.Id), null));
        Assert.Equal(401, guestList.StatusCode);
    }

    // ---- unregister --------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task Unregister_DisablesPush_KeepsTheRecordAndSessions_AndIsRepeatable()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var audit = new RecordingAuditLogWriter();
        var service = NewDeviceService(context, audit);
        var caller = DeviceCaller.ForUser(user.Id, session.Id);
        var request = NewRequest();
        await service.RegisterAsync(caller, request);

        await service.UnregisterCurrentAsync(caller, request.InstallationId);
        await service.UnregisterCurrentAsync(caller, request.InstallationId);

        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.False(row.IsActive);
        Assert.Null(row.PushToken);
        Assert.Null(row.PushTokenHash);
        Assert.Equal(user.Id, row.UserId);
        Assert.Null((await verify.UserSessions.SingleAsync(s => s.Id == session.Id)).RevokedAt);
        Assert.Single(audit.Authentication, e => e.EventType == AuditEvents.Device.Unregistered);
    }

    [SkippableFact]
    public async Task Guest_CanUpdateAndUnregister_ItsOwnInstallationOnly()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var guest = await AddGuestAsync(context);
        var otherGuest = await AddGuestAsync(context);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);

        var updated = await service.UpdateCurrentAsync(
            DeviceCaller.ForGuest(guest.Id), request.InstallationId, new UpdateDeviceRequest { AppVersion = "9.9" });
        var foreign = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.UnregisterCurrentAsync(DeviceCaller.ForGuest(otherGuest.Id), request.InstallationId));
        await service.UnregisterCurrentAsync(DeviceCaller.ForGuest(guest.Id), request.InstallationId);

        Assert.Equal("9.9", updated.AppVersion);
        Assert.Equal(404, foreign.StatusCode);
        await using var verify = _fixture.CreateContext();
        Assert.False((await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).IsActive);
    }

    // ---- logout ------------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task Logout_ReleasesTheInstallation_AndTheNextLoginReEnablesIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var sessions = NewSessionService(context);
        var login = await sessions.CreateAsync(user.Id);
        var sessionId = SessionIdOf(login.RefreshToken);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(user.Id, sessionId), request);

        await sessions.RevokeAsync(login.RefreshToken);

        await using (var verify = _fixture.CreateContext())
        {
            var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
            Assert.False(row.IsActive);
            Assert.Null(row.PushToken);
            Assert.Null(row.UserId); // the installation is released: nothing routes to this account any more
            Assert.Null(row.CurrentSessionId);
            Assert.NotNull((await verify.UserSessions.SingleAsync(s => s.Id == sessionId)).RevokedAt);
        }

        var nextLogin = await NewSessionService(context).CreateAsync(user.Id);
        var again = await service.RegisterAsync(
            DeviceCaller.ForUser(user.Id, SessionIdOf(nextLogin.RefreshToken)), request);
        Assert.False(again.Created);
        Assert.True(again.Device.IsActive);
        Assert.True(again.Device.PushEnabled);
    }

    [SkippableFact]
    public async Task Logout_KeepsPushOn_WhileAnotherLiveSessionUsesTheSameInstallation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var sessions = NewSessionService(context);
        var first = await sessions.CreateAsync(user.Id);
        var second = await sessions.CreateAsync(user.Id);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(user.Id, SessionIdOf(first.RefreshToken)), request);
        await service.RegisterAsync(DeviceCaller.ForUser(user.Id, SessionIdOf(second.RefreshToken)), request);

        await sessions.RevokeAsync(first.RefreshToken);
        await using (var verify = _fixture.CreateContext())
        {
            Assert.True((await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).IsActive);
        }

        await sessions.RevokeAsync(second.RefreshToken);
        await using var final = _fixture.CreateContext();
        Assert.False((await final.Devices.SingleAsync(d => d.InstallationId == request.InstallationId)).IsActive);
    }

    [SkippableFact]
    public async Task Logout_ReleasesTheInstallation_ForTheNextAccountOnTheSamePhone()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var (bob, bobSession) = await AddUserWithSessionAsync(context);
        var sessions = NewSessionService(context);
        var login = await sessions.CreateAsync(alice.Id);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(alice.Id, SessionIdOf(login.RefreshToken)), request);
        await sessions.RevokeAsync(login.RefreshToken);

        var result = await service.RegisterAsync(DeviceCaller.ForUser(bob.Id, bobSession.Id), request);

        Assert.False(result.Created);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(bob.Id, row.UserId);
        Assert.Equal(1, await verify.Devices.CountAsync(d => d.InstallationId == request.InstallationId));
    }

    [SkippableFact]
    public async Task LogoutAll_DisablesEveryInstallationOfTheUser_ButNotOthers()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (other, otherSession) = await AddUserWithSessionAsync(context);
        var sessions = NewSessionService(context);
        var login = await sessions.CreateAsync(user.Id);
        var service = NewDeviceService(context);
        var mine = NewRequest();
        var theirs = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForUser(user.Id, SessionIdOf(login.RefreshToken)), mine);
        await service.RegisterAsync(DeviceCaller.ForUser(other.Id, otherSession.Id), theirs);

        await sessions.RevokeAllAsync(user.Id);

        await using var verify = _fixture.CreateContext();
        Assert.False((await verify.Devices.SingleAsync(d => d.InstallationId == mine.InstallationId)).IsActive);
        Assert.True((await verify.Devices.SingleAsync(d => d.InstallationId == theirs.InstallationId)).IsActive);
    }

    // ---- guest to user -----------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task GuestClaim_TransfersTheInstallation_ToTheNewUser()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestWithTokenAsync(context);
        var request = NewRequest();
        await NewDeviceService(context).RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);

        var result = await NewClaimService(context).ClaimAndDeleteGuestAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Claimed, result);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(user.Id, row.UserId);
        Assert.Null(row.GuestId);
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
    }

    [SkippableFact]
    public async Task GuestClaim_WithAnExpiredGuest_TransfersNothing()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestWithTokenAsync(context, createdDaysAgo: 20);
        var request = NewRequest();
        context.Devices.Add(NewGuestDevice(guest.Id, request));
        await context.SaveChangesAsync();

        var result = await NewClaimService(context).ClaimAndDeleteGuestAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Expired, result);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(guest.Id, row.GuestId);
        Assert.Null(row.UserId);
    }

    [SkippableFact]
    public async Task GuestClaim_FailureRollsBackTheDeviceTransferToo()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestWithTokenAsync(context);
        var request = NewRequest();
        await NewDeviceService(context).RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);
        var failing = new FailingSaveGuestRepository(new GuestRepository(context));
        var service = new GuestMediaClaimService(
            failing,
            Hasher,
            new MediaUploadSessionRepository(context),
            new DeviceRepository(context),
            new GuestVoteClaimRepository(context),
            new RecordingAuditLogWriter());

        var result = await service.TryClaimAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Failed, result);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(guest.Id, row.GuestId);
        Assert.Null(row.UserId);
        Assert.True(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
    }

    [SkippableFact]
    public async Task User_TakingOverAGuestInstallation_NeedsProofOfTheGuestCredential()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (user, session) = await AddUserWithSessionAsync(context);
        var (guest, raw) = await AddGuestWithTokenAsync(context);
        var service = NewDeviceService(context);
        var request = NewRequest();
        await service.RegisterAsync(DeviceCaller.ForGuest(guest.Id), request);

        var refused = await Assert.ThrowsAnyAsync<AppException>(() =>
            service.RegisterAsync(DeviceCaller.ForUser(user.Id, session.Id), request));
        Assert.Equal(409, refused.StatusCode);

        var accepted = await service.RegisterAsync(DeviceCaller.ForUser(user.Id, session.Id, raw), request);
        Assert.False(accepted.Created);
        await using var verify = _fixture.CreateContext();
        var row = await verify.Devices.SingleAsync(d => d.InstallationId == request.InstallationId);
        Assert.Equal(user.Id, row.UserId);
        Assert.Null(row.GuestId);
    }

    [SkippableFact]
    public async Task GuestPurge_RemovesItsInstallations_WithoutViolatingTheForeignKey()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (guest, _) = await AddGuestWithTokenAsync(context, createdDaysAgo: 20);
        var request = NewRequest();
        context.Devices.Add(NewGuestDevice(guest.Id, request));
        await context.SaveChangesAsync();
        var sessions = new MediaUploadSessionRepository(context);
        var cleanup = new GuestCleanupService(
            new GuestRepository(context),
            sessions,
            new MediaUploadCleanupService(sessions, new FakeObjectStorage()),
            GuestCleanupOptions.Default,
            new RecordingAuditLogWriter());

        var result = await cleanup.CleanupAsync();

        Assert.Equal(0, result.GuestsFailed);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
        Assert.False(await verify.Devices.AnyAsync(d => d.InstallationId == request.InstallationId));
    }

    // ---- validation and constraints ----------------------------------------------------------------------------

    [SkippableFact]
    public async Task UntrustedInput_IsValidated()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var guest = await AddGuestAsync(context);
        var service = NewDeviceService(context);
        var caller = DeviceCaller.ForGuest(guest.Id);

        var cases = new List<Action<RegisterDeviceRequest>>
        {
            r => r.Platform = "windows",
            r => r.InstallationId = "",
            r => r.InstallationId = "has space",
            r => r.InstallationId = new string('a', 101),
            r => r.PushToken = new string('t', 4097),
            r => r.PushToken = "tok\nen",
            r => r.DeviceModel = "Pixel\u0000",
            r => r.Locale = new string('l', 21),
            r => r.NotificationPermission = "maybe"
        };
        foreach (var mutate in cases)
        {
            var request = NewRequest();
            mutate(request);
            await Assert.ThrowsAsync<ValidationException>(() => service.RegisterAsync(caller, request));
        }

        await Assert.ThrowsAsync<ValidationException>(() => service.UpdateCurrentAsync(
            caller, "abc", new UpdateDeviceRequest { Timezone = new string('z', 101) }));
        Assert.False(await context.Devices.AnyAsync(d => d.GuestId == guest.Id));
    }

    [SkippableFact]
    public async Task Database_Constraints_EnforceOneOwnerAndOneHolderPerToken()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var guest = await AddGuestAsync(context);
        var now = DateTime.UtcNow;

        var twoOwners = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO devices ("Id","UserId","GuestId","InstallationId","Platform","IsActive","CreatedAt","UpdatedAt")
            VALUES ({Guid.NewGuid()}, {user.Id}, {guest.Id}, {"inst-" + Guid.NewGuid():N}, 'Android', true, {now}, {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, twoOwners.SqlState);

        var token = "dup-" + Guid.NewGuid().ToString("N");
        var hash = Device.ComputeTokenHash(token);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO devices ("Id","GuestId","InstallationId","Platform","IsActive","PushToken","PushTokenHash","CreatedAt","UpdatedAt")
            VALUES ({Guid.NewGuid()}, {guest.Id}, {"inst-" + Guid.NewGuid():N}, 'Android', true, {token}, {hash}, {now}, {now})
            """);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO devices ("Id","GuestId","InstallationId","Platform","IsActive","PushToken","PushTokenHash","CreatedAt","UpdatedAt")
            VALUES ({Guid.NewGuid()}, {guest.Id}, {"inst-" + Guid.NewGuid():N}, 'iOS', true, {token}, {hash}, {now}, {now})
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);

        var mismatched = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO devices ("Id","GuestId","InstallationId","Platform","IsActive","PushToken","CreatedAt","UpdatedAt")
            VALUES ({Guid.NewGuid()}, {guest.Id}, {"inst-" + Guid.NewGuid():N}, 'Android', true, {"no-hash"}, {now}, {now})
            """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, mismatched.SqlState);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    private static DeviceService NewDeviceService(YakkuDbContext context, RecordingAuditLogWriter? audit = null)
    {
        return new DeviceService(
            new DeviceRepository(context),
            new UserSessionRepository(context),
            new GuestRepository(context),
            Hasher,
            new RegisterDeviceValidator(),
            new UpdateDeviceValidator(),
            audit ?? new RecordingAuditLogWriter());
    }

    private static SessionService NewSessionService(YakkuDbContext context)
    {
        return new SessionService(
            new UserSessionRepository(context),
            new TokenService("device-tests-secret-device-tests-secret-1234"),
            new RefreshTokenValidator(),
            new DeviceRepository(context),
            NewDeviceService(context),
            new RecordingAuditLogWriter());
    }

    private static GuestMediaClaimService NewClaimService(YakkuDbContext context)
    {
        return new GuestMediaClaimService(
            new GuestRepository(context),
            Hasher,
            new MediaUploadSessionRepository(context),
            new DeviceRepository(context),
            new GuestVoteClaimRepository(context),
            new RecordingAuditLogWriter());
    }

    private static Guid SessionIdOf(string refreshToken) => Guid.Parse(refreshToken.Split('.')[0]);

    private static RegisterDeviceRequest NewRequest()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new RegisterDeviceRequest
        {
            InstallationId = "inst-" + suffix,
            PushToken = "push-" + suffix,
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

    private static Device NewGuestDevice(Guid guestId, RegisterDeviceRequest request)
    {
        var device = Device.ForGuest(request.InstallationId, DevicePlatform.Android, guestId);
        device.RegisterForGuest(
            guestId, DevicePlatform.Android, request.PushToken, request.DeviceModel, request.OsVersion,
            request.AppVersion, request.AppBuild, request.Locale, request.Timezone, NotificationPermissionStatus.Granted);
        return device;
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var phone = "+1" + string.Concat(suffix.Where(char.IsDigit).Concat("0123456789").Take(10));
        var user = new User(phone, "Device Tester", "anon-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<UserSession> AddSessionAsync(YakkuDbContext context, User user)
    {
        var session = new UserSession(user.Id, "hash-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(30));
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    private static async Task<(User User, UserSession Session)> AddUserWithSessionAsync(YakkuDbContext context)
    {
        var user = await AddUserAsync(context);
        var session = new UserSession(user.Id, "hash-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(30));
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        return (user, session);
    }

    private static async Task<Guest> AddGuestAsync(YakkuDbContext context)
    {
        return (await AddGuestWithTokenAsync(context)).Guest;
    }

    private static async Task<(Guest Guest, string RawToken)> AddGuestWithTokenAsync(
        YakkuDbContext context,
        int createdDaysAgo = 0)
    {
        var raw = "token-" + Guid.NewGuid().ToString("N");
        var guest = new Guest(Hasher.Hash(raw), DateTime.UtcNow.AddDays(-createdDaysAgo));
        context.Guests.Add(guest);
        await context.SaveChangesAsync();
        return (guest, raw);
    }

    /// <summary>Delegates to the real repository but fails the save that deletes the guest.</summary>
    private sealed class FailingSaveGuestRepository : IGuestRepository
    {
        private readonly IGuestRepository _inner;

        public FailingSaveGuestRepository(IGuestRepository inner)
        {
            _inner = inner;
        }

        public Task AddAsync(Guest guest, CancellationToken cancellationToken = default) => _inner.AddAsync(guest, cancellationToken);

        public Task<Guest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _inner.GetByIdAsync(id, cancellationToken);

        public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => _inner.GetByTokenHashAsync(tokenHash, cancellationToken);

        public Task<Guest?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default) => _inner.GetByTokenHashForUpdateAsync(tokenHash, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListExpiredIdsAsync(DateTime expiredBeforeUtc, int batchSize, CancellationToken cancellationToken = default) => _inner.ListExpiredIdsAsync(expiredBeforeUtc, batchSize, cancellationToken);

        public Task<bool> DeleteExpiredByIdAsync(Guid guestId, DateTime expiredBeforeUtc, CancellationToken cancellationToken = default) => _inner.DeleteExpiredByIdAsync(guestId, expiredBeforeUtc, cancellationToken);

        public void Delete(Guest guest) => _inner.Delete(guest);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default) => _inner.ExecuteInTransactionAsync(action, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure while deleting the guest.");
    }
}
