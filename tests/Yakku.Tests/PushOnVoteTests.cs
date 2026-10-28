using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Push;

namespace Yakku.Tests;

public class PushQueueTests
{
    private static VoteNotificationJob Job() => new(Guid.NewGuid(), Guid.NewGuid(), true, "code", Guid.NewGuid());

    private static Channel<VoteNotificationJob> Bounded() =>
        Channel.CreateBounded<VoteNotificationJob>(new BoundedChannelOptions(PushQueueOptions.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    [Fact]
    public async Task Enqueue_WithRoom_Succeeds()
    {
        var channel = Bounded();
        var queue = new PushQueue(channel, NullLogger<PushQueue>.Instance);

        Assert.True(await queue.EnqueueAsync(Job()));
        Assert.True(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Enqueue_WhenFull_DropsAfterTheTimeout_WithoutThrowing()
    {
        var channel = Bounded();
        var queue = new PushQueue(channel, NullLogger<PushQueue>.Instance);
        for (var i = 0; i < PushQueueOptions.Capacity; i++)
        {
            Assert.True(await queue.EnqueueAsync(Job()));
        }

        Assert.False(await queue.EnqueueAsync(Job()));
    }

    [Fact]
    public async Task Enqueue_WhenFull_SucceedsIfTheConsumerFreesASlotInTime()
    {
        var channel = Bounded();
        var queue = new PushQueue(channel, NullLogger<PushQueue>.Instance);
        for (var i = 0; i < PushQueueOptions.Capacity; i++)
        {
            await queue.EnqueueAsync(Job());
        }

        var pending = queue.EnqueueAsync(Job()).AsTask();
        Assert.True(channel.Reader.TryRead(out _));

        Assert.True(await pending);
    }
}

public class PushBackoffTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(10, 30)]
    [InlineData(100, 30)]
    public void Ceiling_DoublesEachAttempt_UpToTheCap(int attempt, double expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), PushQueueOptions.BackoffCeiling(attempt));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.999)]
    public void Jitter_StaysWithinZeroAndTheCeiling(double random)
    {
        for (var attempt = 1; attempt <= 6; attempt++)
        {
            var delay = PushQueueOptions.BackoffWithJitter(attempt, random);
            Assert.InRange(delay, TimeSpan.Zero, PushQueueOptions.BackoffCeiling(attempt));
        }
    }
}

public class VoteNotificationWorkerTests
{
    [Fact]
    public async Task Success_OnFirstAttempt_SavesOneNotification_AndSendsOnce()
    {
        var h = new Harness(new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });

        await h.RunAsync();

        Assert.Equal(1, h.Push.Calls);
        var saved = Assert.Single(h.Notifications.Added);
        Assert.Equal(h.OwnerId, saved.UserId);
        Assert.Equal(Yakku.Domain.Enums.NotificationEventType.PollAnswered, saved.EventType);
    }

    [Fact]
    public async Task TransientFailure_RetriesAndThenSucceeds_WithOneNotificationRow()
    {
        var h = new Harness(
            new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, RetryableFailures = 1 },
            new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });

        await h.RunAsync();

        Assert.Equal(2, h.Push.Calls);
        Assert.Single(h.Notifications.Added);
    }

    [Fact]
    public async Task TransientFailure_StopsAfterMaxAttempts()
    {
        var h = new Harness(new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, RetryableFailures = 1 });

        await h.RunAsync();

        Assert.Equal(PushQueueOptions.MaxAttempts, h.Push.Calls);
        Assert.Single(h.Notifications.Added);
    }

    [Fact]
    public async Task PartialSuccess_IsNotRetried()
    {
        var h = new Harness(
            new PushUserNotificationResult { EligibleDevices = 2, Successful = 1, Failed = 1, RetryableFailures = 1 });

        await h.RunAsync();

        Assert.Equal(1, h.Push.Calls);
    }

    [Fact]
    public async Task InvalidToken_IsNotRetried()
    {
        var h = new Harness(
            new PushUserNotificationResult { EligibleDevices = 1, Failed = 1, InvalidTokensDeactivated = 1 });

        await h.RunAsync();

        Assert.Equal(1, h.Push.Calls);
    }

    [Fact]
    public async Task NoEligibleDevice_IsNotRetried_ButTheNotificationIsStillSaved()
    {
        var h = new Harness(new AppException(400, "VALIDATION_ERROR", "No device with an active login session."));

        await h.RunAsync();

        Assert.Equal(1, h.Push.Calls);
        Assert.Single(h.Notifications.Added);
    }

    [Fact]
    public async Task ProviderFault_IsRetried()
    {
        var h = new Harness(
            new AppException(502, "INTERNAL_SERVER_ERROR", "Failed to send push notification."),
            new PushUserNotificationResult { EligibleDevices = 1, Successful = 1 });

        await h.RunAsync();

        Assert.Equal(2, h.Push.Calls);
    }

    private sealed class Harness
    {
        public Harness(params object[] script)
        {
            Push = new ScriptedPushService(script);
            Notifications = new CapturingNotificationRepository();
        }

        public Guid OwnerId { get; } = Guid.NewGuid();
        public ScriptedPushService Push { get; }
        public CapturingNotificationRepository Notifications { get; }

        public async Task RunAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IPushNotificationService>(Push);
            services.AddSingleton<INotificationRepository>(Notifications);
            var provider = services.BuildServiceProvider();

            var channel = Channel.CreateBounded<VoteNotificationJob>(PushQueueOptions.Capacity);
            await channel.Writer.WriteAsync(new VoteNotificationJob(OwnerId, Guid.NewGuid(), true, "code", Guid.NewGuid()));
            channel.Writer.Complete();

            var worker = new VoteNotificationWorker(
                channel,
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<VoteNotificationWorker>.Instance);

            // The channel is completed, so the worker drains the job (including retry delays) and returns.
            await worker.StartAsync(CancellationToken.None);
            await worker.ExecuteTask!.WaitAsync(TimeSpan.FromMinutes(2));
        }
    }

    private sealed class ScriptedPushService : IPushNotificationService
    {
        private readonly object[] _script;

        public ScriptedPushService(object[] script) => _script = script;

        public int Calls { get; private set; }

        public Task<PushUserNotificationResult> SendToUserAsync(
            Guid userId, NotificationPayload payload, CancellationToken cancellationToken = default)
        {
            // The last scripted step repeats once the script runs out.
            var step = _script[Math.Min(Calls, _script.Length - 1)];
            Calls++;
            return step is Exception exception
                ? Task.FromException<PushUserNotificationResult>(exception)
                : Task.FromResult((PushUserNotificationResult)step);
        }

        public Task<PushDeviceNotificationResult> SendToDeviceAsync(Device device, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendToDevicesAsync(IReadOnlyList<Device> devices, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendToDeviceAsync(string installationId, string platform, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushDeviceNotificationResult> SendTestToDeviceAsync(TestDeviceNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PushUserNotificationResult> SendTestToUserAsync(TestUserNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, DateTime? cursorCreatedAt, Guid? cursorId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Notification?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
