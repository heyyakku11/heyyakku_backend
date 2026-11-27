using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yakku.Application.AskBack;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.AskBack.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Jobs;
using Yakku.Infrastructure.Persistence;
using static Yakku.Tests.AskBackIntegrationTests;

namespace Yakku.Tests;

/// <summary>
/// Real-PostgreSQL checks for Ask Back delivery tracking: the lifecycle, who may acknowledge, idempotency,
/// concurrency, timeouts and database failures.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AskBackDeliveryTrackingTests
{
    private readonly PostgresFixture _fixture;

    public AskBackDeliveryTrackingTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task SentMessage_IsTrackedBeforeTheSend_AsSent_AndAnAcknowledgmentMakesItDelivered()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var seenDuringSend = new List<AskBackDeliveryStatus?>();
        var realtime = new ObservingRealtime(async (userId, message) =>
        {
            if (userId != scene.Voter.Id)
            {
                return;
            }

            // What another connection sees while the send is in flight: the row is stored and already Sent.
            await using var other = _fixture.CreateContext();
            seenDuringSend.Add(await other.AskBackDeliveries.AsNoTracking()
                .Where(d => d.MessageId == message.Id)
                .Select(d => (AskBackDeliveryStatus?)d.Status)
                .FirstOrDefaultAsync());
        });
        var service = ServiceFor(context, realtime, new AckCapable(scene.Voter.Id));

        var sent = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("Why that one?"));

        Assert.Equal([AskBackDeliveryStatus.Sent], seenDuringSend);
        var row = await RowAsync(sent.Id);
        Assert.Equal(scene.Voter.Id, row.RecipientUserId);
        Assert.Equal(AskBackDeliveryStatus.Sent, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Null(row.DeliveredAt);
        Assert.Equal(sent.CreatedAt, row.CreatedAt, TimeSpan.FromMilliseconds(1));

        var outcome = await service.AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id);

        Assert.Equal(AskBackAckOutcome.Delivered, outcome);
        row = await RowAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.Delivered, row.Status);
        Assert.NotNull(row.DeliveredAt);
        Assert.True(row.DeliveredAt >= row.SentAt);
        Assert.Equal(row.DeliveredAt!.Value, row.UpdatedAt, TimeSpan.FromMilliseconds(1));
    }

    [SkippableFact]
    public async Task FailedSignalRDelivery_IsRecordedAsFailed_AndStillReachesTheSenderEcho()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var realtime = new RecordingRealtime { ThrowFor = userId => userId == scene.Voter.Id };
        var logger = new CapturingLogger<AskBackService>();

        var sent = await ServiceFor(context, realtime, new AckCapable(scene.Voter.Id), logger)
            .SendMessageAsync(scene.Asker.Id, conversationId, Send("Still saved"));

        var row = await RowAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.Failed, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Null(row.DeliveredAt);
        Assert.Equal(scene.Asker.Id, Assert.Single(realtime.Messages).UserId);
        Assert.Contains(logger.Entries, e => e.StartsWith("Warning") && e.Contains("delivery attempt failed")
                                             && e.Contains(sent.Id.ToString()) && e.Contains("hub down"));
        // The message content never reaches the logs.
        Assert.DoesNotContain("Still saved", logger.AllText);
    }

    [SkippableFact]
    public async Task RecipientWithoutAnAckCapableConnection_IsTrackedSeparately_AndStillSentTo()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var realtime = new RecordingRealtime();

        var sent = await ServiceFor(context, realtime)
            .SendMessageAsync(scene.Asker.Id, conversationId, Send("Offline?"));

        var row = await RowAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.NoAckConnection, row.Status);
        Assert.NotNull(row.SentAt);
        Assert.Contains(realtime.Messages, m => m.UserId == scene.Voter.Id);
    }

    [SkippableFact]
    public async Task DuplicateAcknowledgment_ChangesTheRowOnce()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId, sent, service) = await SentAsync(context);

        Assert.Equal(AskBackAckOutcome.Delivered, await service.AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id));
        var first = await RowAsync(sent.Id);
        await Task.Delay(20);

        Assert.Equal(AskBackAckOutcome.AlreadyDelivered, await service.AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id));
        var second = await RowAsync(sent.Id);
        Assert.Equal(first.DeliveredAt, second.DeliveredAt);
        Assert.Equal(first.UpdatedAt, second.UpdatedAt);
        Assert.Equal(1, await context.AskBackDeliveries.CountAsync(d => d.MessageId == sent.Id));
    }

    [SkippableFact]
    public async Task Acknowledgment_ByTheSender_AnOutsider_OrForAnUnknownMessage_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _, sent, service) = await SentAsync(context);
        var outsider = await AddUserAsync(context);
        var logger = new CapturingLogger<AskBackService>();
        var logged = ServiceFor(context, logger: logger);

        Assert.Equal(AskBackAckOutcome.NotFound, await logged.AcknowledgeDeliveryAsync(scene.Asker.Id, sent.Id));
        Assert.Equal(AskBackAckOutcome.NotFound, await logged.AcknowledgeDeliveryAsync(outsider.Id, sent.Id));
        Assert.Equal(AskBackAckOutcome.NotFound, await logged.AcknowledgeDeliveryAsync(scene.Voter.Id, Guid.NewGuid()));
        Assert.Equal(AskBackAckOutcome.NotFound, await logged.AcknowledgeDeliveryAsync(Guid.Empty, sent.Id));

        Assert.Equal(AskBackDeliveryStatus.Sent, (await RowAsync(sent.Id)).Status);
        Assert.Equal(4, logger.Entries.Count(e => e.StartsWith("Warning") && e.Contains("invalid acknowledgment rejected")));
    }

    [SkippableFact]
    public async Task ConcurrentAcknowledgments_ProduceExactlyOneTransition()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _, sent, _) = await SentAsync(context);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var own = _fixture.CreateContext();
            return await ServiceFor(own).AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id);
        }));

        Assert.Equal(1, outcomes.Count(o => o == AskBackAckOutcome.Delivered));
        Assert.Equal(9, outcomes.Count(o => o == AskBackAckOutcome.AlreadyDelivered));
        Assert.Equal(AskBackDeliveryStatus.Delivered, (await RowAsync(sent.Id)).Status);
    }

    [SkippableFact]
    public async Task MissingAcknowledgment_TimesOut_AndALateAcknowledgmentStillCounts()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _, sent, service) = await SentAsync(context);
        var originalSentAt = await BackdateAsync(context, sent.Id, TimeSpan.FromMinutes(5));
        var logger = new CapturingLogger<AskBackDeliveryTimeoutSweeper>();

        await SweeperFor(context, logger).SweepAsync(default);

        var row = await RowAsync(sent.Id);
        Assert.Equal(AskBackDeliveryStatus.TimedOut, row.Status);
        Assert.Equal(originalSentAt, row.SentAt);
        Assert.Null(row.DeliveredAt);
        Assert.Contains(logger.Entries, e => e.StartsWith("Warning") && e.Contains("acknowledgment timeout detected")
                                             && e.Contains(sent.Id.ToString()));

        // The acknowledgment was only late: the message did arrive.
        Assert.Equal(AskBackAckOutcome.Delivered, await service.AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id));
        Assert.Equal(AskBackDeliveryStatus.Delivered, (await RowAsync(sent.Id)).Status);
    }

    [SkippableFact]
    public async Task Sweeper_FailsStalePending_AndLeavesFreshAndDeliveredRowsAlone()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId, stale, service) = await SentAsync(context);
        // As if the process stopped between the commit and the send.
        var old = DateTime.UtcNow.AddMinutes(-5);
        await context.AskBackDeliveries.Where(d => d.MessageId == stale.Id).ExecuteUpdateAsync(set => set
            .SetProperty(d => d.Status, AskBackDeliveryStatus.Pending)
            .SetProperty(d => d.SentAt, (DateTime?)null)
            .SetProperty(d => d.CreatedAt, old));
        var reply = await service.SendMessageAsync(scene.Voter.Id, conversationId, Send("fresh reply"));
        var delivered = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("delivered"));
        Assert.Equal(AskBackAckOutcome.Delivered, await service.AcknowledgeDeliveryAsync(scene.Voter.Id, delivered.Id));
        await BackdateAsync(context, delivered.Id, TimeSpan.FromMinutes(5));

        await SweeperFor(context).SweepAsync(default);

        Assert.Equal(AskBackDeliveryStatus.Failed, (await RowAsync(stale.Id)).Status);
        Assert.Equal(AskBackDeliveryStatus.Sent, (await RowAsync(reply.Id)).Status);
        Assert.Equal(AskBackDeliveryStatus.Delivered, (await RowAsync(delivered.Id)).Status);
    }

    [SkippableFact]
    public async Task RetriedSend_WithTheSameClientMessageId_CreatesNoSecondTrackingRowOrDelivery()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var realtime = new RecordingRealtime();
        var service = ServiceFor(context, realtime, new AckCapable(scene.Voter.Id));
        var request = Send("only once");

        var first = await service.SendMessageAsync(scene.Asker.Id, conversationId, request);
        var retry = await service.SendMessageAsync(scene.Asker.Id, conversationId, request);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await context.AskBackDeliveries.CountAsync(d => d.MessageId == first.Id));
        Assert.Single(realtime.Messages, m => m.UserId == scene.Voter.Id);
    }

    [SkippableFact]
    public async Task UniqueIndex_RefusesASecondTrackingRowForTheSameRecipient()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _, sent, _) = await SentAsync(context);

        context.AskBackDeliveries.Add(AskBackMessageDeliveryTracking.Create(sent.Id, scene.Voter.Id, DateTime.UtcNow));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [SkippableFact]
    public async Task TrackingUpdateFailure_IsLogged_AndDoesNotStopTheDelivery()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        await using var failing = FailingTrackingContext();
        var realtime = new RecordingRealtime();
        var logger = new CapturingLogger<AskBackService>();

        var sent = await ServiceFor(failing, realtime, new AckCapable(scene.Voter.Id), logger)
            .SendMessageAsync(scene.Asker.Id, conversationId, Send("still delivered"));

        Assert.Contains(realtime.Messages, m => m.UserId == scene.Voter.Id && m.Message.Id == sent.Id);
        // The row was stored with the message and stays Pending for the sweeper to settle.
        Assert.Equal(AskBackDeliveryStatus.Pending, (await RowAsync(sent.Id)).Status);
        Assert.Contains(logger.Entries, e => e.StartsWith("Error") && e.Contains("could not be set to")
                                             && e.Contains(sent.Id.ToString()));
    }

    [SkippableFact]
    public async Task AcknowledgmentUpdateFailure_Throws_IsLogged_AndLeavesTheRowUnchanged()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _, sent, _) = await SentAsync(context);
        await using var failing = FailingTrackingContext();
        var logger = new CapturingLogger<AskBackService>();

        await Assert.ThrowsAnyAsync<Exception>(
            () => ServiceFor(failing, logger: logger).AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id));

        Assert.Equal(AskBackDeliveryStatus.Sent, (await RowAsync(sent.Id)).Status);
        Assert.Contains(logger.Entries, e => e.StartsWith("Error") && e.Contains("acknowledgment could not be saved"));

        // Sent again once the database is back, it is recorded.
        Assert.Equal(AskBackAckOutcome.Delivered, await ServiceFor(context).AcknowledgeDeliveryAsync(scene.Voter.Id, sent.Id));
    }

    [Fact]
    public void Options_RejectNonsenseSettings()
    {
        Assert.Empty(new AskBackDeliveryOptions().Validate());
        var errors = new AskBackDeliveryOptions { AckTimeoutSeconds = 0, SweepIntervalSeconds = 0, SweepBatchSize = 0 }.Validate();
        Assert.Equal(3, errors.Count);
        Assert.Equal(TimeSpan.FromSeconds(45), new AskBackDeliveryOptions { AckTimeoutSeconds = 45 }.AckTimeout);
    }

    // ---- helpers ----

    /// <summary>A conversation with one asker message tracked as Sent to the voter.</summary>
    private async Task<(Scene Scene, Guid ConversationId, AskBackMessageResponse Sent, AskBackService Service)> SentAsync(
        YakkuDbContext context)
    {
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context, presence: new AckCapable(scene.Voter.Id, scene.Asker.Id));
        var sent = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("Tracked"));
        Assert.Equal(AskBackDeliveryStatus.Sent, (await RowAsync(sent.Id)).Status);
        return (scene, conversationId, sent, service);
    }

    private async Task<AskBackMessageDeliveryTracking> RowAsync(Guid messageId)
    {
        await using var context = _fixture.CreateContext();
        return await context.AskBackDeliveries.AsNoTracking().SingleAsync(d => d.MessageId == messageId);
    }

    /// <summary>Moves the attempt into the past; returns the new sent_at.</summary>
    private static async Task<DateTime> BackdateAsync(YakkuDbContext context, Guid messageId, TimeSpan by)
    {
        var sentAt = DateTime.UtcNow - by;
        await context.AskBackDeliveries.Where(d => d.MessageId == messageId)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.SentAt, (DateTime?)sentAt));
        return (await context.AskBackDeliveries.AsNoTracking().SingleAsync(d => d.MessageId == messageId)).SentAt!.Value;
    }

    private static AskBackDeliveryTimeoutSweeper SweeperFor(
        YakkuDbContext context,
        Microsoft.Extensions.Logging.ILogger<AskBackDeliveryTimeoutSweeper>? logger = null)
    {
        return new AskBackDeliveryTimeoutSweeper(
            context,
            Options.Create(new AskBackDeliveryOptions { AckTimeoutSeconds = 30, SweepBatchSize = 50 }),
            TimeProvider.System,
            logger ?? NullLogger<AskBackDeliveryTimeoutSweeper>.Instance);
    }

    /// <summary>A context whose updates of the tracking table fail, as if the database went away mid-request.</summary>
    private YakkuDbContext FailingTrackingContext()
    {
        var options = new DbContextOptionsBuilder<YakkuDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .AddInterceptors(new FailTrackingUpdates())
            .Options;
        return new YakkuDbContext(options);
    }

    private sealed class FailTrackingUpdates : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.TrimStart().StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("ask_back_message_delivery_tracking", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("database unavailable");
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>These users have a connection that acknowledges.</summary>
    private sealed class AckCapable(params Guid[] userIds) : IAskBackPresence
    {
        public bool IsViewing(Guid conversationId, Guid userId) => false;

        public bool HasAckCapableConnection(Guid userId) => userIds.Contains(userId);
    }

    private sealed class ObservingRealtime(Func<Guid, AskBackMessageResponse, Task> onSend) : IAskBackRealtime
    {
        public Task SendMessageToUserAsync(Guid userId, AskBackMessageResponse message, CancellationToken cancellationToken = default)
            => onSend(userId, message);

        public Task MessageReadAsync(Guid conversationId, Guid? otherUserId, Guid lastReadMessageId, DateTime readAt, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StatusChangedAsync(Guid conversationId, IReadOnlyCollection<Guid> userIds, string status, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
