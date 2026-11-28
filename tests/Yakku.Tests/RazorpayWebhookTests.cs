using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Payments;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

public sealed class RazorpayWebhookSignatureTests
{
    [Fact]
    public void IsValid_AcceptsMatchingHmacOverRawBody()
    {
        var body = Encoding.UTF8.GetBytes("""{"event":"payment.captured","id":"evt_1"}""");
        var secret = "whsec_test_secret";
        var signature = RazorpayWebhookSignature.ComputeHexHmacSha256(body, secret);

        Assert.True(RazorpayWebhookSignature.IsValid(body, signature, secret));
        Assert.False(RazorpayWebhookSignature.IsValid(body, signature, "other_secret"));
        Assert.False(RazorpayWebhookSignature.IsValid(body, "deadbeef", secret));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class RazorpayWebhookProcessorTests
{
    private const string WebhookSecret = "whsec_test_value_not_logged";
    private readonly PostgresFixture _fixture;

    public RazorpayWebhookProcessorTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Receiver_RejectsInvalidSignature()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (receiver, _) = BuildStack(context);

        var eventId = $"evt_bad_{Guid.NewGuid():N}";
        var body = Encoding.UTF8.GetBytes($"{{\"event\":\"payment.captured\",\"id\":\"{eventId}\"}}");
        var result = await receiver.ReceiveAsync(body, "invalid", eventId, "corr");

        Assert.False(result.Accepted);
        Assert.Equal(ApiErrorCodes.WebhookSignatureInvalid, result.ErrorCode);
        Assert.Equal(0, await context.WebhookEvents.CountAsync(e => e.RazorpayEventId == eventId));
    }

    [SkippableFact]
    public async Task Receiver_EnqueuesAndDedupesByEventId()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (receiver, signal) = BuildStack(context);

        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = $"{{\"event\":\"payment.authorized\",\"id\":\"{eventId}\",\"payload\":{{}}}}";
        var body = Encoding.UTF8.GetBytes(payload);
        var signature = RazorpayWebhookSignature.ComputeHexHmacSha256(body, WebhookSecret);

        var first = await receiver.ReceiveAsync(body, signature, eventId, "c1");
        var second = await receiver.ReceiveAsync(body, signature, eventId, "c2");

        Assert.True(first.Accepted);
        Assert.False(first.Duplicate);
        Assert.True(second.Accepted);
        Assert.True(second.Duplicate);
        Assert.Equal(1, await context.WebhookEvents.CountAsync(e => e.RazorpayEventId == eventId));
        Assert.True(signal.Reader.TryRead(out _));
    }

    [SkippableFact]
    public async Task Processor_PaymentCaptured_FulfillsLitePlan()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        var eventId = $"evt_{Guid.NewGuid():N}";

        var payload = BuildPaymentPayload(
            "payment.captured",
            eventId,
            paymentId,
            order.RazorpayOrderId!,
            order.AmountInPaise,
            order.Currency,
            captured: true,
            status: "captured");

        var inbox = new RazorpayWebhookInboxRepository(context);
        var now = DateTime.UtcNow;
        var (stored, _) = await inbox.TryEnqueueAsync(eventId, "payment.captured", payload, now);
        stored.MarkProcessing();
        await context.SaveChangesAsync();

        var processor = ProcessorFor(context);
        await processor.ProcessAsync(stored.Id);

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLiteCode, user.UserPlan.PlanCode);
        Assert.Equal(1, await context.UserPlanEntitlements.CountAsync(e => e.UserId == user.Id));
        Assert.Equal(
            WebhookProcessingStatus.Processed,
            await context.WebhookEvents.Where(e => e.Id == stored.Id).Select(e => e.ProcessingStatus).SingleAsync());
        Assert.Equal(
            PaymentAttemptStatus.Captured,
            await context.PaymentAttempts.Where(a => a.RazorpayPaymentId == paymentId).Select(a => a.Status).SingleAsync());
    }

    [SkippableFact]
    public async Task Processor_PaymentFailed_AfterCapture_DoesNotDowngrade()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";

        var fulfillmentRepo = new PaymentFulfillmentRepository(context);
        var fulfillment = new PaymentFulfillmentService(fulfillmentRepo, new NullPaymentLogWriter());
        await fulfillment.FulfillCapturedPaymentAsync(
            new CapturedPaymentFulfillmentCommand(
                order.Id,
                order.RazorpayOrderId!,
                paymentId,
                order.AmountInPaise,
                order.Currency,
                "upi",
                "captured",
                DateTime.UtcNow,
                "seed-capture"));

        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = BuildPaymentPayload(
            "payment.failed",
            eventId,
            paymentId,
            order.RazorpayOrderId!,
            order.AmountInPaise,
            order.Currency,
            captured: false,
            status: "failed");

        var inbox = new RazorpayWebhookInboxRepository(context);
        var (stored, _) = await inbox.TryEnqueueAsync(eventId, "payment.failed", payload, DateTime.UtcNow);
        stored.MarkProcessing();
        await context.SaveChangesAsync();

        await ProcessorFor(context).ProcessAsync(stored.Id);

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLiteCode, user.UserPlan.PlanCode);
        Assert.Equal(
            PaymentAttemptStatus.Captured,
            await context.PaymentAttempts.Where(a => a.RazorpayPaymentId == paymentId).Select(a => a.Status).SingleAsync());
        Assert.Equal(
            WebhookProcessingStatus.Processed,
            await context.WebhookEvents.Where(e => e.Id == stored.Id).Select(e => e.ProcessingStatus).SingleAsync());
    }

    [SkippableFact]
    public async Task Processor_PaymentFailed_RecordsAttemptWithoutFailingOrder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        var eventId = $"evt_{Guid.NewGuid():N}";
        var payload = BuildPaymentPayload(
            "payment.failed",
            eventId,
            paymentId,
            order.RazorpayOrderId!,
            order.AmountInPaise,
            order.Currency,
            captured: false,
            status: "failed");

        var inbox = new RazorpayWebhookInboxRepository(context);
        var (stored, _) = await inbox.TryEnqueueAsync(eventId, "payment.failed", payload, DateTime.UtcNow);
        stored.MarkProcessing();
        await context.SaveChangesAsync();

        await ProcessorFor(context).ProcessAsync(stored.Id);

        await context.Entry(order).ReloadAsync();
        Assert.Equal(PaymentOrderStatus.Pending, order.Status);
        Assert.Equal(
            PaymentAttemptStatus.Failed,
            await context.PaymentAttempts.Where(a => a.RazorpayPaymentId == paymentId).Select(a => a.Status).SingleAsync());
        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.FreeCode, user.UserPlan.PlanCode);
    }

    private static (RazorpayWebhookReceiver Receiver, RazorpayWebhookSignal Signal) BuildStack(YakkuDbContext context)
    {
        var signal = new RazorpayWebhookSignal();
        var options = RazorpayOptions.FromConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Razorpay:Enabled"] = "true",
                    ["Razorpay:KeyId"] = "rzp_test_key",
                    ["Razorpay:KeySecret"] = "key_secret",
                    ["Razorpay:WebhookSecret"] = WebhookSecret
                })
                .Build());

        var receiver = new RazorpayWebhookReceiver(
            new RazorpayWebhookInboxRepository(context),
            new NullPaymentLogWriter(),
            options,
            TimeProvider.System,
            NullLogger<RazorpayWebhookReceiver>.Instance,
            signal);

        return (receiver, signal);
    }

    private static RazorpayWebhookProcessor ProcessorFor(YakkuDbContext context)
    {
        var fulfillmentRepo = new PaymentFulfillmentRepository(context);
        return new RazorpayWebhookProcessor(
            new RazorpayWebhookInboxRepository(context),
            new PaymentOrderRepository(context),
            new PaymentFulfillmentService(fulfillmentRepo, new NullPaymentLogWriter()),
            fulfillmentRepo,
            new NullPaymentLogWriter(),
            TimeProvider.System,
            NullLogger<RazorpayWebhookProcessor>.Instance);
    }

    private static string BuildPaymentPayload(
        string eventType,
        string eventId,
        string paymentId,
        string orderId,
        int amount,
        string currency,
        bool captured,
        string status)
    {
        var payload = new
        {
            @event = eventType,
            id = eventId,
            payload = new
            {
                payment = new
                {
                    entity = new
                    {
                        id = paymentId,
                        order_id = orderId,
                        amount,
                        currency,
                        status,
                        captured,
                        method = "upi"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private async Task<PaymentOrder> SeedPendingOrderAsync(YakkuDbContext context, User user, Guid planId)
    {
        var plan = await context.Plans.AsNoTracking().SingleAsync(p => p.Id == planId);
        var order = PaymentOrder.CreateUserPlan(
            user.Id,
            plan.Id,
            plan.PriceMinor,
            plan.Currency,
            PlanPurchaseSnapshot.FromPlan(plan).ToJson(),
            $"idem-{Guid.NewGuid():N}",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(15),
            "webhook-seed");
        order.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task<User> SeedUserAsync(YakkuDbContext context)
    {
        var user = new User($"+9198{Random.Shared.Next(10000000, 99999999)}", $"wh-{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
