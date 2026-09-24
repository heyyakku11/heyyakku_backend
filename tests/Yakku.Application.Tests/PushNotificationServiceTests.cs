using FluentValidation;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.PushNotifications.Services;
using Yakku.Application.PushNotifications.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.PushNotifications;

public class PushNotificationServiceTests
{
    [Fact]
    public async Task SendToDevice_ActiveDeviceWithToken_Succeeds()
    {
        var fixture = Fixture.Create();
        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var device = CreateDevice(user.Id, "install-1", "token-a");
        fixture.Devices.Items.Add(device);

        var result = await fixture.Service.SendToDeviceAsync(
            "install-1",
            "android",
            new NotificationPayload { Title = "Hello", Body = "World" });

        Assert.True(result.Success);
        Assert.Equal("msg-token-a", result.FirebaseMessageId);
        Assert.Equal(1, fixture.Firebase.SendCalls);
    }

    [Fact]
    public async Task SendToDevice_MissingDevice_ThrowsNotFound()
    {
        var fixture = Fixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SendToDeviceAsync(
                "missing",
                "android",
                new NotificationPayload { Title = "Hello", Body = "World" }));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task SendToDevice_InactiveDevice_ThrowsValidationError()
    {
        var fixture = Fixture.Create();
        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var device = CreateDevice(user.Id, "install-1", "token-a");
        device.Deactivate();
        fixture.Devices.Items.Add(device);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SendToDeviceAsync(
                "install-1",
                "android",
                new NotificationPayload { Title = "Hello", Body = "World" }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("inactive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendToDevice_MissingPushToken_ThrowsValidationError()
    {
        var fixture = Fixture.Create();
        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var device = CreateDevice(user.Id, "install-1", pushToken: null);
        fixture.Devices.Items.Add(device);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SendToDeviceAsync(
                "install-1",
                "android",
                new NotificationPayload { Title = "Hello", Body = "World" }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Contains("push token", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendToUser_FiltersInactiveAndMissingTokens()
    {
        var fixture = Fixture.Create();
        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var activeA = CreateDevice(user.Id, "a", "token-a");
        var activeB = CreateDevice(user.Id, "b", "token-b");
        var inactive = CreateDevice(user.Id, "c", "token-c");
        inactive.Deactivate();
        var noToken = CreateDevice(user.Id, "d", pushToken: null);

        fixture.Devices.Items.AddRange([activeA, activeB, inactive, noToken]);

        var result = await fixture.Service.SendToUserAsync(
            user.Id,
            new NotificationPayload { Title = "Multi", Body = "Devices" });

        Assert.Equal(4, result.TotalDevices);
        Assert.Equal(2, result.EligibleDevices);
        Assert.Equal(2, result.Successful);
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, fixture.Firebase.MulticastTokenCounts.Single());
    }

    [Fact]
    public async Task SendToUser_InvalidToken_DeactivatesOnlyThatDevice()
    {
        var fixture = Fixture.Create();
        fixture.Firebase.InvalidTokens.Add("token-bad");

        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var good = CreateDevice(user.Id, "good", "token-good");
        var bad = CreateDevice(user.Id, "bad", "token-bad");
        fixture.Devices.Items.AddRange([good, bad]);

        var result = await fixture.Service.SendToUserAsync(
            user.Id,
            new NotificationPayload { Title = "Partial", Body = "Failure" });

        Assert.Equal(2, result.EligibleDevices);
        Assert.Equal(1, result.Successful);
        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.InvalidTokensDeactivated);
        Assert.True(good.IsActive);
        Assert.False(bad.IsActive);
        Assert.Null(bad.PushToken);
    }

    [Fact]
    public async Task SendToUser_NoRegisteredDevices_ThrowsNotFound()
    {
        var fixture = Fixture.Create();
        var user = new User("user@example.com", "push-user");
        fixture.Users.Items.Add(user);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SendToUserAsync(
                user.Id,
                new NotificationPayload { Title = "Hello", Body = "World" }));

        Assert.Equal(404, exception.StatusCode);
    }

    private static Device CreateDevice(Guid userId, string installationId, string? pushToken)
    {
        var device = new Device(installationId, DevicePlatform.Android, userId);
        device.Register(
            userId,
            DevicePlatform.Android,
            pushToken,
            deviceModel: null,
            osVersion: null,
            appVersion: null,
            appBuild: null,
            locale: null,
            timezone: null,
            pushToken is null
                ? NotificationPermissionStatus.Denied
                : NotificationPermissionStatus.Granted);
        return device;
    }

    private sealed class Fixture
    {
        public required PushNotificationService Service { get; init; }
        public required FakeDeviceRepository Devices { get; init; }
        public required FakeUserRepository Users { get; init; }
        public required FakeFirebaseProvider Firebase { get; init; }

        public static Fixture Create()
        {
            var devices = new FakeDeviceRepository();
            var users = new FakeUserRepository();
            var firebase = new FakeFirebaseProvider();

            return new Fixture
            {
                Devices = devices,
                Users = users,
                Firebase = firebase,
                Service = new PushNotificationService(
                    firebase,
                    devices,
                    users,
                    new NotificationPayloadValidator(),
                    new TestDeviceNotificationRequestValidator(),
                    new TestUserNotificationRequestValidator())
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
            IReadOnlyList<Device> matches = Items.Where(device => device.UserId == userId).ToList();
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

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(user => user.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(user => user.Email == email));
        }

        public Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.Any(user => user.Profile.DisplayName == displayName));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeFirebaseProvider : IFirebaseNotificationProvider
    {
        public int SendCalls { get; private set; }
        public List<int> MulticastTokenCounts { get; } = [];
        public HashSet<string> InvalidTokens { get; } = new(StringComparer.Ordinal);

        public Task<FirebaseTokenSendResult> SendAsync(
            string pushToken,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            SendCalls++;
            if (InvalidTokens.Contains(pushToken))
            {
                return Task.FromResult(new FirebaseTokenSendResult
                {
                    TokenFingerprint = "invalid",
                    Success = false,
                    IsInvalidToken = true,
                    ErrorCode = "Unregistered"
                });
            }

            return Task.FromResult(new FirebaseTokenSendResult
            {
                TokenFingerprint = "ok",
                Success = true,
                MessageId = $"msg-{pushToken}"
            });
        }

        public Task<IReadOnlyList<FirebaseTokenSendResult>> SendToManyAsync(
            IReadOnlyList<string> pushTokens,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            MulticastTokenCounts.Add(pushTokens.Count);
            IReadOnlyList<FirebaseTokenSendResult> results = pushTokens
                .Select(token =>
                {
                    if (InvalidTokens.Contains(token))
                    {
                        return new FirebaseTokenSendResult
                        {
                            TokenFingerprint = "invalid",
                            Success = false,
                            IsInvalidToken = true,
                            ErrorCode = "Unregistered"
                        };
                    }

                    return new FirebaseTokenSendResult
                    {
                        TokenFingerprint = "ok",
                        Success = true,
                        MessageId = $"msg-{token}"
                    };
                })
                .ToList();

            return Task.FromResult(results);
        }
    }
}
