using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PaymentPhase6Tests
{
    private readonly PostgresFixture _fixture;

    public PaymentPhase6Tests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task History_IsOwnerScoped()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await SeedUserAsync(context);
        var other = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, owner, Plan.YakkuLiteId);

        var queries = new PaymentQueryService(new PaymentQueryRepository(context));
        var (items, meta) = await queries.GetHistoryAsync(owner.Id, page: 1, pageSize: 10);
        Assert.Contains(items, item => item.OrderId == order.Id);
        Assert.True(meta.TotalCount >= 1);

        var (otherItems, _) = await queries.GetHistoryAsync(other.Id, page: 1, pageSize: 10);
        Assert.DoesNotContain(otherItems, item => item.OrderId == order.Id);
    }

    [SkippableFact]
    public async Task Reconciliation_ExpiresStaleOpenOrders()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);

        await context.PaymentOrders.Where(o => o.Id == order.Id).ExecuteUpdateAsync(set => set
            .SetProperty(o => o.ExpiresAt, DateTime.UtcNow.AddMinutes(-5)));
        context.ChangeTracker.Clear();

        var service = new PaymentReconciliationService(
            new PaymentReconciliationRepository(context),
            new Phase6FakeRazorpay(),
            new PaymentFulfillmentService(new PaymentFulfillmentRepository(context), new NullPaymentLogWriter()),
            new NullPaymentLogWriter(),
            TimeProvider.System,
            NullLogger<PaymentReconciliationService>.Instance);

        var result = await service.RunAsync();
        Assert.True(result.ExpiredOrders >= 1);

        context.ChangeTracker.Clear();
        Assert.Equal(
            PaymentOrderStatus.Expired,
            await context.PaymentOrders.AsNoTracking().Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
    }

    [SkippableFact]
    public async Task Reconciliation_FulfillsCapturedGatewayPaymentOnPendingOrder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_recon_{Guid.NewGuid():N}";

        var razorpay = new Phase6FakeRazorpay
        {
            OrderPayments =
            {
                [order.RazorpayOrderId!] =
                [
                    new RazorpayPaymentResult(
                        true,
                        paymentId,
                        order.RazorpayOrderId,
                        order.AmountInPaise,
                        order.Currency,
                        "captured",
                        true,
                        "upi",
                        null,
                        null)
                ]
            }
        };

        var service = new PaymentReconciliationService(
            new PaymentReconciliationRepository(context),
            razorpay,
            new PaymentFulfillmentService(new PaymentFulfillmentRepository(context), new NullPaymentLogWriter()),
            new NullPaymentLogWriter(),
            TimeProvider.System,
            NullLogger<PaymentReconciliationService>.Instance);

        var result = await service.RunAsync();
        Assert.True(result.CapturedFulfillments >= 1);

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLiteCode, user.UserPlan.PlanCode);
        Assert.Equal(
            PaymentOrderStatus.Paid,
            await context.PaymentOrders.Where(o => o.Id == order.Id).Select(o => o.Status).SingleAsync());
    }

    [SkippableFact]
    public async Task Refund_ProcessesCapturedAttempt_WithoutRevokingAccess()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_ref_{Guid.NewGuid():N}";

        var fulfillment = new PaymentFulfillmentService(
            new PaymentFulfillmentRepository(context), new NullPaymentLogWriter());
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
                "phase6"));

        var attemptId = await context.PaymentAttempts
            .Where(a => a.RazorpayPaymentId == paymentId)
            .Select(a => a.Id)
            .SingleAsync();

        var razorpay = new Phase6FakeRazorpay { RefundSucceeds = true };
        var refunds = new PaymentRefundService(
            new PaymentRefundRepository(context),
            razorpay,
            new NullPaymentLogWriter(),
            TimeProvider.System);

        var response = await refunds.CreateRefundAsync(
            attemptId,
            new CreatePaymentRefundRequest { Reason = "test refund" },
            "corr-refund");

        Assert.Equal(PaymentRefundStatus.Processed.ToString(), response.Status);
        Assert.False(string.IsNullOrWhiteSpace(response.RazorpayRefundId));
        Assert.Contains("not automatically revoke", response.AccessNote, StringComparison.OrdinalIgnoreCase);

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLiteCode, user.UserPlan.PlanCode);
        Assert.Equal(
            1,
            await context.UserPlanEntitlements.CountAsync(e =>
                e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active));
    }

    [SkippableFact]
    public async Task Refund_RejectsNonCapturedAttempt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var attempt = PaymentAttempt.Create(
            order.Id,
            order.AmountInPaise,
            order.Currency,
            DateTime.UtcNow,
            $"pay_fail_{Guid.NewGuid():N}",
            "upi",
            "failed");
        attempt.MarkFailed(DateTime.UtcNow, "failed", "declined", "failed");
        context.PaymentAttempts.Add(attempt);
        await context.SaveChangesAsync();

        var refunds = new PaymentRefundService(
            new PaymentRefundRepository(context),
            new Phase6FakeRazorpay { RefundSucceeds = true },
            new NullPaymentLogWriter(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            refunds.CreateRefundAsync(attempt.Id, new CreatePaymentRefundRequest(), "corr"));
        Assert.Equal(ApiErrorCodes.ValidationError, error.ErrorCode);
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
            "phase6");
        order.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task<User> SeedUserAsync(YakkuDbContext context)
    {
        var user = new User($"+9195{Random.Shared.Next(10000000, 99999999)}", $"p6-{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class Phase6FakeRazorpay : IRazorpayClient
    {
        public Dictionary<string, IReadOnlyList<RazorpayPaymentResult>> OrderPayments { get; } = new();
        public bool RefundSucceeds { get; set; }
        public bool IsEnabled => true;

        public Task<RazorpayOrderResult> CreateOrderAsync(
            RazorpayCreateOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayOrderResult(true, $"order_{Guid.NewGuid():N}", request.AmountInPaise, request.Currency, "created", null, null));
        }

        public Task<RazorpayPaymentResult> GetPaymentAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayPaymentResult(false, null, null, null, null, null, false, null, "not_found", "missing"));
        }

        public Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                OrderPayments.TryGetValue(razorpayOrderId, out var list)
                    ? list
                    : Array.Empty<RazorpayPaymentResult>());
        }

        public Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!RefundSucceeds)
            {
                return Task.FromResult(
                    new RazorpayRefundResult(false, null, null, null, "failed", "refund failed"));
            }

            return Task.FromResult(
                new RazorpayRefundResult(
                    true,
                    $"rfnd_{Guid.NewGuid():N}",
                    "processed",
                    request.AmountInPaise,
                    null,
                    null));
        }
    }
}
