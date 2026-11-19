using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Payments;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;
using Yakku.Application.Payments.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class PaymentVerificationServiceTests
{
    private const string KeySecret = "test_secret_value_not_logged";
    private readonly PostgresFixture _fixture;

    public PaymentVerificationServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Verify_CapturedLitePayment_ActivatesPlan()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        razorpay.Payments[paymentId] = Captured(paymentId, order.RazorpayOrderId!, order.AmountInPaise, order.Currency);

        var service = VerifyServiceFor(context, razorpay);
        var result = await service.VerifyAsync(
            user.Id,
            SignedRequest(order.RazorpayOrderId!, paymentId),
            "verify-1");

        Assert.False(result.AlreadyFulfilled);
        Assert.Equal(PaymentOrderStatus.Paid.ToString(), result.Status);
        Assert.Equal(Plan.YakkuLiteCode, result.PlanCode);
        Assert.NotNull(result.PlanExpiresAt);

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLiteId, user.PlanId);
        Assert.Equal(1, await context.UserPlanEntitlements.CountAsync(e => e.UserId == user.Id));
        Assert.Equal(1, await context.PaymentAttempts.CountAsync(a => a.RazorpayPaymentId == paymentId));
    }

    [SkippableFact]
    public async Task Verify_DuplicateCallback_IsIdempotent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLoveId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        razorpay.Payments[paymentId] = Captured(paymentId, order.RazorpayOrderId!, order.AmountInPaise, order.Currency);
        var service = VerifyServiceFor(context, razorpay);
        var request = SignedRequest(order.RazorpayOrderId!, paymentId);

        var first = await service.VerifyAsync(user.Id, request, "v1");
        var second = await service.VerifyAsync(user.Id, request, "v2");

        Assert.False(first.AlreadyFulfilled);
        Assert.True(second.AlreadyFulfilled);
        Assert.Equal(1, await context.UserPlanEntitlements.CountAsync(e => e.UserId == user.Id));
        Assert.Equal(1, await context.PaymentAttempts.CountAsync(a => a.RazorpayPaymentId == paymentId));
    }

    [SkippableFact]
    public async Task Verify_InvalidSignature_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var service = VerifyServiceFor(context, razorpay);

        var error = await Assert.ThrowsAsync<AppException>(() => service.VerifyAsync(
            user.Id,
            new VerifyPaymentRequest
            {
                RazorpayOrderId = order.RazorpayOrderId,
                RazorpayPaymentId = $"pay_{Guid.NewGuid():N}",
                RazorpaySignature = "not-a-valid-signature"
            },
            "bad-sig"));

        Assert.Equal(ApiErrorCodes.PaymentSignatureInvalid, error.ErrorCode);
        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.FreeId, user.PlanId);
    }

    [SkippableFact]
    public async Task Verify_AuthorizedButNotCaptured_DoesNotGrantAccess()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        razorpay.Payments[paymentId] = new RazorpayPaymentResult(
            true, paymentId, order.RazorpayOrderId, order.AmountInPaise, order.Currency,
            "authorized", false, "upi", null, null);

        var service = VerifyServiceFor(context, razorpay);
        var error = await Assert.ThrowsAsync<AppException>(() => service.VerifyAsync(
            user.Id, SignedRequest(order.RazorpayOrderId!, paymentId), "auth-only"));

        Assert.Equal(ApiErrorCodes.PaymentNotCaptured, error.ErrorCode);
        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.FreeId, user.PlanId);
    }

    [SkippableFact]
    public async Task Verify_WrongAmount_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLiteId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        razorpay.Payments[paymentId] = Captured(paymentId, order.RazorpayOrderId!, order.AmountInPaise + 1, order.Currency);

        var service = VerifyServiceFor(context, razorpay);
        var error = await Assert.ThrowsAsync<AppException>(() => service.VerifyAsync(
            user.Id, SignedRequest(order.RazorpayOrderId!, paymentId), "amt"));

        Assert.Equal(ApiErrorCodes.PaymentAmountMismatch, error.ErrorCode);
    }

    [SkippableFact]
    public async Task Verify_Love_SupersedesActiveLite()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var lite = await context.Plans.SingleAsync(p => p.Id == Plan.YakkuLiteId);
        user.ActivatePlan(lite, DateTime.UtcNow);
        context.UserPlanEntitlements.Add(UserPlanEntitlement.Activate(
            user.Id, Plan.YakkuLiteId, DateTime.UtcNow, DateTime.UtcNow.AddDays(7)));
        await context.SaveChangesAsync();

        var (order, razorpay) = await SeedPendingOrderAsync(context, user, Plan.YakkuLoveId);
        var paymentId = $"pay_{Guid.NewGuid():N}";
        razorpay.Payments[paymentId] = Captured(paymentId, order.RazorpayOrderId!, order.AmountInPaise, order.Currency);

        var service = VerifyServiceFor(context, razorpay);
        await service.VerifyAsync(user.Id, SignedRequest(order.RazorpayOrderId!, paymentId), "upgrade");

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLoveId, user.PlanId);
        Assert.Equal(
            1,
            await context.UserPlanEntitlements.CountAsync(
                e => e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active));
        Assert.Equal(
            1,
            await context.UserPlanEntitlements.CountAsync(
                e => e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Superseded));
    }

    private static VerifyPaymentRequest SignedRequest(string orderId, string paymentId)
    {
        return new VerifyPaymentRequest
        {
            RazorpayOrderId = orderId,
            RazorpayPaymentId = paymentId,
            RazorpaySignature = RazorpayPaymentSignature.ComputeHexHmacSha256($"{orderId}|{paymentId}", KeySecret)
        };
    }

    private static RazorpayPaymentResult Captured(string paymentId, string orderId, int amount, string currency)
    {
        return new RazorpayPaymentResult(
            true, paymentId, orderId, amount, currency, "captured", true, "upi", null, null);
    }

    private async Task<(PaymentOrder Order, FakeRazorpayClient Razorpay)> SeedPendingOrderAsync(
        YakkuDbContext context,
        User user,
        Guid planId)
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
            "seed");
        order.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return (order, new FakeRazorpayClient());
    }

    private static PaymentVerificationService VerifyServiceFor(YakkuDbContext context, FakeRazorpayClient razorpay)
    {
        var options = RazorpayOptions.FromConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Razorpay:Enabled"] = "true",
                    ["Razorpay:KeyId"] = "rzp_test_key",
                    ["Razorpay:KeySecret"] = KeySecret,
                    ["Razorpay:WebhookSecret"] = "whsec_test"
                })
                .Build());

        var fulfillmentRepo = new PaymentFulfillmentRepository(context);
        return new PaymentVerificationService(
            new PaymentOrderRepository(context),
            fulfillmentRepo,
            new PaymentFulfillmentService(fulfillmentRepo, new NullPaymentLogWriter()),
            razorpay,
            new NullPaymentLogWriter(),
            options,
            new VerifyPaymentValidator(),
            TimeProvider.System,
            NullLogger<PaymentVerificationService>.Instance);
    }

    private static async Task<User> SeedUserAsync(YakkuDbContext context)
    {
        var user = new User($"+9197{Random.Shared.Next(10000000, 99999999)}", $"pay-{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class FakeRazorpayClient : IRazorpayClient
    {
        public Dictionary<string, RazorpayPaymentResult> Payments { get; } = new();
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
            if (Payments.TryGetValue(razorpayPaymentId, out var payment))
            {
                return Task.FromResult(payment);
            }

            return Task.FromResult(
                new RazorpayPaymentResult(
                    false, null, null, null, null, null, false, null, "not_found", "missing"));
        }

        public Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RazorpayPaymentResult>>(Payments.Values.ToList());
        }

        public Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayRefundResult(false, null, null, null, "not_implemented", "not used in verify tests"));
        }
    }
}
