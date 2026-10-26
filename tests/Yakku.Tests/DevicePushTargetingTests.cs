using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Yakku.API.Controllers;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.PushNotifications.Services;
using Yakku.Application.PushNotifications.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

public class DevicesControllerContractTests
{
    [Fact]
    public void TheControllerRequiresSignIn_AndOffersNoAnonymousRoutes()
    {
        var type = typeof(DevicesController);

        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(type.GetCustomAttribute<AllowAnonymousAttribute>());
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
        }
    }

    [Fact]
    public void ThereIsNoDeleteRoute_OnlyRegisterAndUpdate()
    {
        var routes = typeof(DevicesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>())
            .Select(attribute => $"{attribute.HttpMethods.Single()} {attribute.Template}")
            .Order()
            .ToList();

        Assert.Equal(["PATCH me", "POST register"], routes);
    }
}

public class DevicePushTargetingTests
{
    [Fact]
    public async Task Push_GoesOnlyToDevicesWithALiveSession()
    {
        var h = new Harness();
        var phone = h.AddDevice("phone", withLiveSession: true);
        var tablet = h.AddDevice("tablet", withLiveSession: true);
        var loggedOutLaptop = h.AddDevice("laptop", withLiveSession: false);

        var result = await h.Push.SendToUserAsync(h.UserId, Payload());

        Assert.Equal(3, result.TotalDevices);
        Assert.Equal(2, result.EligibleDevices);
        Assert.Equal(2, result.Successful);
        Assert.Equal(
            new[] { phone.PushToken!, tablet.PushToken! }.Order(),
            h.Firebase.SentTokens.Order());
        Assert.DoesNotContain(loggedOutLaptop.PushToken!, h.Firebase.SentTokens);
    }

    [Fact]
    public async Task ADeviceWithoutAPushToken_IsNotAPushTarget_EvenWithALiveSession()
    {
        var h = new Harness();
        var withToken = h.AddDevice("phone", withLiveSession: true);
        h.AddDevice("tokenless", withLiveSession: true, pushToken: null);

        var result = await h.Push.SendToUserAsync(h.UserId, Payload());

        Assert.Equal(1, result.EligibleDevices);
        Assert.Equal([withToken.PushToken!], h.Firebase.SentTokens);
    }

    [Fact]
    public async Task WhenEverySessionHasEnded_NothingIsSent_AndTheCallerIsTold()
    {
        var h = new Harness();
        h.AddDevice("phone", withLiveSession: false);
        h.AddDevice("tablet", withLiveSession: false);

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Push.SendToUserAsync(h.UserId, Payload()));

        Assert.Equal(400, exception.StatusCode);
        Assert.Empty(h.Firebase.SentTokens);
    }

    [Fact]
    public async Task AUserWithNoDevicesAtAll_Returns404()
    {
        var h = new Harness();

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Push.SendToUserAsync(h.UserId, Payload()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task AnotherUsersLiveDevices_AreNeverTargeted()
    {
        var h = new Harness();
        var mine = h.AddDevice("mine", withLiveSession: true);
        h.AddDevice("theirs", withLiveSession: true, owner: Guid.NewGuid());

        await h.Push.SendToUserAsync(h.UserId, Payload());

        Assert.Equal([mine.PushToken!], h.Firebase.SentTokens);
    }

    private static NotificationPayload Payload() => new() { Title = "Hello", Body = "A friend answered your poll." };

    private sealed class Harness
    {
        private int _counter;

        public Harness()
        {
            UserId = Guid.NewGuid();
            Devices = new InMemoryDeviceRepository();
            Firebase = new RecordingFirebase();
            Push = new PushNotificationService(
                Firebase,
                Devices,
                new KnownUser(UserId),
                new NotificationPayloadValidator(),
                new TestDeviceNotificationRequestValidator(),
                new TestUserNotificationRequestValidator(),
                new RecordingAuditLogWriter());
        }

        public Guid UserId { get; }
        public InMemoryDeviceRepository Devices { get; }
        public RecordingFirebase Firebase { get; }
        public PushNotificationService Push { get; }

        public Device AddDevice(string installationId, bool withLiveSession, string? pushToken = "auto", Guid? owner = null)
        {
            var ownerId = owner ?? UserId;
            var device = new Device(installationId, DevicePlatform.Android, ownerId);
            device.Register(
                ownerId,
                DevicePlatform.Android,
                pushToken == "auto" ? $"token-{++_counter}-{installationId}" : pushToken,
                null, null, null, null, null, null,
                NotificationPermissionStatus.Granted);
            Devices.Seed(device);
            if (withLiveSession)
            {
                Devices.DevicesWithLiveSession.Add(device.Id);
            }

            return device;
        }
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

    private sealed class KnownUser : IUserRepository
    {
        private readonly User _user;

        public KnownUser(Guid id)
        {
            _user = new User("+15551230000", "Sam", "Quiet Fox");
            typeof(User).GetProperty(nameof(User.Id))!.SetValue(_user, id);
        }

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(id == _user.Id ? _user : null);

        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
            => Task.FromResult<User?>(null);

        public Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

/// <summary>The real session query behind "push only to devices with a live session". Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DevicePushTargetsIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public DevicePushTargetsIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task OnlyDevicesBoundToALiveSession_AreTargets()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var now = DateTime.UtcNow;

        var live = await AddDeviceAsync(context, user, "live");
        var revoked = await AddDeviceAsync(context, user, "revoked");
        var expired = await AddDeviceAsync(context, user, "expired");
        var unbound = await AddDeviceAsync(context, user, "unbound");
        var noToken = await AddDeviceAsync(context, user, "no-token", pushToken: null);
        var twoSessions = await AddDeviceAsync(context, user, "two-sessions");

        await AddSessionAsync(context, user, live, now.AddDays(10));
        var revokedSession = await AddSessionAsync(context, user, revoked, now.AddDays(10));
        revokedSession.Revoke();
        await AddSessionAsync(context, user, expired, now.AddMinutes(-1));
        await AddSessionAsync(context, user, noToken, now.AddDays(10));
        var endedSecond = await AddSessionAsync(context, user, twoSessions, now.AddDays(10));
        endedSecond.Revoke();
        await AddSessionAsync(context, user, twoSessions, now.AddDays(10)); // one live session is enough
        await context.SaveChangesAsync();

        var targets = await new DeviceRepository(context).GetPushTargetsByUserIdAsync(user.Id, now);

        Assert.Equal(
            new[] { live.Id, twoSessions.Id }.Order(),
            targets.Select(device => device.Id).Order());
        Assert.DoesNotContain(targets, device => device.Id == unbound.Id || device.Id == noToken.Id);
    }

    [SkippableFact]
    public async Task ADeviceOfAnotherUser_IsNeverATarget_EvenWithALiveSessionOfThatUser()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var bobsPhone = await AddDeviceAsync(context, bob, "bobs-phone");
        await AddSessionAsync(context, bob, bobsPhone, DateTime.UtcNow.AddDays(10));
        await context.SaveChangesAsync();

        var targets = await new DeviceRepository(context).GetPushTargetsByUserIdAsync(alice.Id, DateTime.UtcNow);

        Assert.Empty(targets);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"557{sequence:D10}", $"Pusher {sequence}", $"push-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Device> AddDeviceAsync(
        YakkuDbContext context,
        User user,
        string name,
        string? pushToken = "auto")
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var device = new Device($"push-{name}-{sequence}", DevicePlatform.Android, user.Id);
        device.Register(
            user.Id,
            DevicePlatform.Android,
            pushToken == "auto" ? $"push-token-{name}-{sequence}-{Guid.NewGuid():N}" : pushToken,
            null, null, null, null, null, null,
            NotificationPermissionStatus.Granted);
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device;
    }

    private static async Task<UserSession> AddSessionAsync(
        YakkuDbContext context,
        User user,
        Device device,
        DateTime expiresAt)
    {
        var session = new UserSession(user.Id, "hash-" + Guid.NewGuid().ToString("N"), expiresAt);
        session.AssignDevice(device.Id);
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        device.AssignToUser(user.Id, session.Id);
        await context.SaveChangesAsync();
        return session;
    }
}
