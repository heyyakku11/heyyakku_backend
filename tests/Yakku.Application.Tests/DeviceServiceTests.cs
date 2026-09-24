using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Devices.Services;
using Yakku.Application.Devices.Validators;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.Devices;

public class DeviceServiceTests
{
    [Fact]
    public async Task Register_NewDevice_CreatesActiveDevice()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        var result = await fixture.Service.RegisterAsync(userId, NewRegisterRequest(), sessionId);

        Assert.True(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal(userId, fixture.Devices.Items[0].UserId);
        Assert.True(result.Device.IsActive);
        Assert.Equal("install-1", result.Device.InstallationId);
        Assert.Equal("android", result.Device.Platform);
        Assert.Equal("Pixel 8", fixture.Devices.Items[0].DeviceModel);
        Assert.Equal("granted", result.Device.NotificationPermission);
        Assert.NotNull(result.Device.LastSeenAt);
        Assert.Null(typeof(DeviceResponse).GetProperty("PushToken"));
        Assert.Null(typeof(DeviceResponse).GetProperty("UserId"));
        Assert.Equal("fcm-token", fixture.Devices.Items[0].PushToken);
        Assert.Equal(fixture.Devices.Items[0].Id, fixture.Sessions.Items[0].DeviceId);
        Assert.Single(fixture.Preferences.Items);
        Assert.Equal(userId, fixture.Preferences.Items[0].UserId);
        Assert.True(fixture.Preferences.Items[0].PushEnabled);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.DeviceRegistered);
    }

    [Fact]
    public async Task Register_SameInstallationTwice_UpdatesExistingDevice()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest(), sessionId);

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "new-token", appVersion: "1.1.0"),
            sessionId);

        Assert.False(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal("new-token", fixture.Devices.Items[0].PushToken);
        Assert.Equal("1.1.0", fixture.Devices.Items[0].AppVersion);
        Assert.True(result.Device.IsActive);
        Assert.Equal(fixture.Devices.Items[0].Id, fixture.Sessions.Items[0].DeviceId);
        Assert.Single(fixture.Preferences.Items);
    }

    [Fact]
    public async Task Register_GrantedWithPushToken_CreatesNotificationPreferences()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        await fixture.Service.RegisterAsync(userId, NewRegisterRequest(), sessionId);

        Assert.Single(fixture.Preferences.Items);
        Assert.Equal(userId, fixture.Preferences.Items[0].UserId);
        Assert.True(fixture.Preferences.Items[0].PushEnabled);
        Assert.True(fixture.Preferences.Items[0].PollActivityEnabled);
        Assert.True(fixture.Preferences.Items[0].OffersEnabled);
        Assert.True(fixture.Preferences.Items[0].AlertsEnabled);
        Assert.True(fixture.Preferences.Items[0].NormalEnabled);
    }

    [Fact]
    public async Task Register_GrantedTwice_DoesNotDuplicateNotificationPreferences()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest(), sessionId);

        await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "another-token"),
            sessionId);

        Assert.Single(fixture.Preferences.Items);
    }

    [Fact]
    public async Task Register_DeniedPermission_DoesNotCreateNotificationPreferences()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "should-clear", permission: "denied"),
            sessionId);

        Assert.Empty(fixture.Preferences.Items);
    }

    [Fact]
    public async Task Register_MissingPushToken_DoesNotCreateNotificationPreferences()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: null),
            sessionId);

        Assert.Empty(fixture.Preferences.Items);
    }

    [Fact]
    public async Task Register_ReassignsInstallationToCurrentUser()
    {
        var fixture = DeviceFixture.Create();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        var firstSessionId = fixture.AddSession(firstUser);
        var secondSessionId = fixture.AddSession(secondUser);
        await fixture.Service.RegisterAsync(firstUser, NewRegisterRequest(), firstSessionId);

        var result = await fixture.Service.RegisterAsync(secondUser, NewRegisterRequest(), secondSessionId);

        Assert.False(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal(secondUser, fixture.Devices.Items[0].UserId);
        Assert.Equal(fixture.Devices.Items[0].Id, fixture.Sessions.Items.Single(s => s.Id == secondSessionId).DeviceId);
        Assert.Null(typeof(DeviceResponse).GetProperty("UserId"));
    }

    [Fact]
    public async Task Register_DeniedPermission_ClearsPushTokenAndStaysActive()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "should-clear", permission: "denied"),
            sessionId);

        Assert.True(result.Device.IsActive);
        Assert.Equal("denied", result.Device.NotificationPermission);
        Assert.Null(fixture.Devices.Items[0].PushToken);
        Assert.Null(typeof(DeviceResponse).GetProperty("PushToken"));
        Assert.Empty(fixture.Preferences.Items);
    }

    [Fact]
    public async Task Register_WithSessionId_AssignsDeviceToSession()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(),
            sessionId);

        Assert.True(result.Created);
        Assert.Equal(fixture.Devices.Items[0].Id, fixture.Sessions.Items[0].DeviceId);
    }

    [Fact]
    public async Task Register_UnknownSessionId_ReturnsUnauthorized()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RegisterAsync(userId, NewRegisterRequest(), Guid.NewGuid()));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Empty(fixture.Devices.Items);
    }

    [Fact]
    public async Task Register_ForeignSessionId_ReturnsUnauthorized()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var foreignSessionId = fixture.AddSession(otherUserId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RegisterAsync(userId, NewRegisterRequest(), foreignSessionId));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
        Assert.Empty(fixture.Devices.Items);
        Assert.Null(fixture.Sessions.Items[0].DeviceId);
    }

    [Fact]
    public async Task Register_EmptySessionId_ReturnsUnauthorized()
    {
        var fixture = DeviceFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.RegisterAsync(Guid.NewGuid(), NewRegisterRequest(), Guid.Empty));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.Unauthorized, exception.ErrorCode);
    }

    [Fact]
    public async Task Register_InvalidPlatform_ThrowsValidationException()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        var sessionId = fixture.AddSession(userId);

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.RegisterAsync(
                userId,
                NewRegisterRequest(platform: "windows"),
                sessionId));
    }

    private static RegisterDeviceRequest NewRegisterRequest(
        string installationId = "install-1",
        string platform = "android",
        string? pushToken = "fcm-token",
        string? permission = "granted",
        string? appVersion = "1.0.0")
    {
        return new RegisterDeviceRequest
        {
            InstallationId = installationId,
            Platform = platform,
            PushToken = pushToken,
            DeviceModel = "Pixel 8",
            OsVersion = "15",
            AppVersion = appVersion,
            AppBuild = "1",
            Locale = "en-IN",
            Timezone = "Asia/Kolkata",
            NotificationPermission = permission
        };
    }

    private sealed class DeviceFixture
    {
        public required DeviceService Service { get; init; }
        public required FakeDeviceRepository Devices { get; init; }
        public required FakeUserSessionRepository Sessions { get; init; }
        public required FakeNotificationPreferenceRepository Preferences { get; init; }
        public required FakeSystemLogWriter Logs { get; init; }

        public Guid AddSession(Guid userId)
        {
            var session = new UserSession(
                userId,
                $"hash-{Guid.NewGuid():N}",
                DateTime.UtcNow.AddDays(7));
            Sessions.Items.Add(session);
            return session.Id;
        }

        public static DeviceFixture Create()
        {
            var devices = new FakeDeviceRepository();
            var sessions = new FakeUserSessionRepository();
            var preferences = new FakeNotificationPreferenceRepository();
            var logs = new FakeSystemLogWriter();

            return new DeviceFixture
            {
                Service = new DeviceService(
                    devices,
                    sessions,
                    preferences,
                    logs,
                    new RegisterDeviceValidator()),
                Devices = devices,
                Sessions = sessions,
                Preferences = preferences,
                Logs = logs
            };
        }
    }

    private sealed class FakeNotificationPreferenceRepository
        : Yakku.Application.NotificationPreferences.Interfaces.INotificationPreferenceRepository
    {
        public List<NotificationPreference> Items { get; } = [];

        public Task<NotificationPreference?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(preference => preference.UserId == userId));
        }

        public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
        {
            Items.Add(preference);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserSessionRepository : Yakku.Application.Auth.Interfaces.IUserSessionRepository
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

        public Task DeleteAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Items.RemoveAll(session => session.UserId == userId);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeviceRepository : IDeviceRepository
    {
        public List<Device> Items { get; } = [];

        public Task<Device?> GetByInstallationIdAsync(
            string installationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(device => device.InstallationId == installationId));
        }

        public Task<Device?> GetByInstallationIdAndPlatformAsync(
            string installationId,
            DevicePlatform platform,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(
                device => device.InstallationId == installationId && device.Platform == platform));
        }

        public Task<IReadOnlyList<Device>> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Device> matches = Items
                .Where(device => device.UserId == userId)
                .ToList();
            return Task.FromResult(matches);
        }

        public Task AddAsync(Device device, CancellationToken cancellationToken = default)
        {
            Items.Add(device);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
