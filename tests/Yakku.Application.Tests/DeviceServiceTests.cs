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
using Xunit;

namespace Yakku.Application.Tests.Devices;

public class DeviceServiceTests
{
    [Fact]
    public async Task Register_NewDevice_CreatesActiveDevice()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();

        var result = await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        Assert.True(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal(userId, fixture.Devices.Items[0].UserId);
        Assert.True(result.Device.IsActive);
        Assert.Equal("install-1", result.Device.InstallationId);
        Assert.Equal("android", result.Device.Platform);
        Assert.Equal("Pixel 8", result.Device.DeviceModel);
        Assert.Equal("granted", result.Device.NotificationPermission);
        Assert.NotNull(result.Device.LastSeenAt);
        Assert.Null(typeof(DeviceResponse).GetProperty("PushToken"));
        Assert.Null(typeof(DeviceResponse).GetProperty("UserId"));
        Assert.Equal("fcm-token", fixture.Devices.Items[0].PushToken);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.DeviceRegistered);
    }

    [Fact]
    public async Task Register_SameInstallationTwice_UpdatesExistingDevice()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "new-token", appVersion: "1.1.0"));

        Assert.False(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal("new-token", fixture.Devices.Items[0].PushToken);
        Assert.Equal("1.1.0", result.Device.AppVersion);
        Assert.True(result.Device.IsActive);
    }

    [Fact]
    public async Task Register_ReassignsInstallationToCurrentUser()
    {
        var fixture = DeviceFixture.Create();
        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();
        await fixture.Service.RegisterAsync(firstUser, NewRegisterRequest());

        var result = await fixture.Service.RegisterAsync(secondUser, NewRegisterRequest());

        Assert.False(result.Created);
        Assert.Single(fixture.Devices.Items);
        Assert.Equal(secondUser, fixture.Devices.Items[0].UserId);
        Assert.Null(typeof(DeviceResponse).GetProperty("UserId"));
    }

    [Fact]
    public async Task Register_AfterDeactivate_ReactivatesAndRestoresToken()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());
        await fixture.Service.UnregisterAsync(userId, "install-1");

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "restored-token"));

        Assert.False(result.Created);
        Assert.True(result.Device.IsActive);
        Assert.Equal("restored-token", fixture.Devices.Items[0].PushToken);
        Assert.Single(fixture.Devices.Items);
    }

    [Fact]
    public async Task Register_DeniedPermission_ClearsPushTokenAndStaysActive()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();

        var result = await fixture.Service.RegisterAsync(
            userId,
            NewRegisterRequest(pushToken: "should-clear", permission: "denied"));

        Assert.True(result.Device.IsActive);
        Assert.Equal("denied", result.Device.NotificationPermission);
        Assert.Null(fixture.Devices.Items[0].PushToken);
        Assert.Null(typeof(DeviceResponse).GetProperty("PushToken"));
    }

    [Fact]
    public async Task Update_SuppliedFieldsOnly_LeavesOthersUnchanged()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        var result = await fixture.Service.UpdateAsync(
            userId,
            "install-1",
            new UpdateDeviceRequest { AppVersion = "2.0.0" });

        Assert.Equal("2.0.0", result.AppVersion);
        Assert.Equal("1", result.AppBuild);
        Assert.Equal("Pixel 8", result.DeviceModel);
        Assert.Equal("android", result.Platform);
        Assert.Equal("fcm-token", fixture.Devices.Items[0].PushToken);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.DeviceUpdated);
    }

    [Fact]
    public async Task Update_EmptyPushToken_ClearsToken()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        await fixture.Service.UpdateAsync(
            userId,
            "install-1",
            new UpdateDeviceRequest { PushToken = "" });

        Assert.Null(fixture.Devices.Items[0].PushToken);
        Assert.True(fixture.Devices.Items[0].IsActive);
    }

    [Fact]
    public async Task Update_DeniedPermission_ClearsPushToken()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        var result = await fixture.Service.UpdateAsync(
            userId,
            "install-1",
            new UpdateDeviceRequest { NotificationPermission = "denied" });

        Assert.True(result.IsActive);
        Assert.Equal("denied", result.NotificationPermission);
        Assert.Null(fixture.Devices.Items[0].PushToken);
    }

    [Fact]
    public async Task Update_AnotherUsersDevice_ReturnsNotFound()
    {
        var fixture = DeviceFixture.Create();
        var ownerId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(ownerId, NewRegisterRequest());

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.UpdateAsync(
                Guid.NewGuid(),
                "install-1",
                new UpdateDeviceRequest { AppVersion = "9.9.9" }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.Equal("1.0.0", fixture.Devices.Items[0].AppVersion);
    }

    [Fact]
    public async Task Update_MissingDevice_ReturnsNotFound()
    {
        var fixture = DeviceFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.UpdateAsync(
                Guid.NewGuid(),
                "missing",
                new UpdateDeviceRequest { AppVersion = "1.0.1" }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Unregister_DeactivatesAndClearsPushToken()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());

        await fixture.Service.UnregisterAsync(userId, "install-1");

        var device = fixture.Devices.Items[0];
        Assert.False(device.IsActive);
        Assert.Null(device.PushToken);
        Assert.Equal(userId, device.UserId);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.DeviceUnregistered);
    }

    [Fact]
    public async Task Unregister_AlreadyInactive_Succeeds()
    {
        var fixture = DeviceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(userId, NewRegisterRequest());
        await fixture.Service.UnregisterAsync(userId, "install-1");

        await fixture.Service.UnregisterAsync(userId, "install-1");

        Assert.Single(fixture.Devices.Items);
        Assert.False(fixture.Devices.Items[0].IsActive);
    }

    [Fact]
    public async Task Unregister_AnotherUsersDevice_ReturnsNotFound()
    {
        var fixture = DeviceFixture.Create();
        var ownerId = Guid.NewGuid();
        await fixture.Service.RegisterAsync(ownerId, NewRegisterRequest());

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.UnregisterAsync(Guid.NewGuid(), "install-1"));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.True(fixture.Devices.Items[0].IsActive);
        Assert.Equal("fcm-token", fixture.Devices.Items[0].PushToken);
    }

    [Fact]
    public async Task Register_InvalidPlatform_ThrowsValidationException()
    {
        var fixture = DeviceFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.RegisterAsync(
                Guid.NewGuid(),
                NewRegisterRequest(platform: "windows")));
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
        public required FakeSystemLogWriter Logs { get; init; }

        public static DeviceFixture Create()
        {
            var devices = new FakeDeviceRepository();
            var logs = new FakeSystemLogWriter();

            return new DeviceFixture
            {
                Service = new DeviceService(
                    devices,
                    logs,
                    new RegisterDeviceValidator(),
                    new UpdateDeviceValidator()),
                Devices = devices,
                Logs = logs
            };
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

        public Task<Device?> GetByInstallationIdAndUserIdAsync(
            string installationId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Items.FirstOrDefault(device =>
                    device.InstallationId == installationId && device.UserId == userId));
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
