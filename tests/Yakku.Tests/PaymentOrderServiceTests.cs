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
public sealed class PaymentOrderServiceTests
{
    private readonly PostgresFixture _fixture;

    public PaymentOrderServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task CreateOrder_Lite_ReturnsCheckout_WithoutGrantingEntitlement()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var razorpay = new FakeRazorpayClient();
        var service = ServiceFor(context, razorpay);

        var lite = await context.Plans.AsNoTracking().SingleAsync(p => p.Id == Plan.YakkuLiteId);
        var result = await service.CreateOrderAsync(
            user.Id,
            new CreatePaymentOrderRequest
            {
                PlanId = Plan.YakkuLiteId,
                IdempotencyKey = $"lite-{Guid.NewGuid():N}"
            },
            correlationId: "test-1");

        Assert.StartsWith("order_test_", result.RazorpayOrderId);
        Assert.Equal("rzp_test_key", result.RazorpayKeyId);
        Assert.Equal(lite.PriceMinor, result.AmountInPaise);
        Assert.True(result.AmountInPaise > 0);
        Assert.Equal("INR", result.Currency);
        Assert.Equal(Plan.YakkuLiteCode, result.PlanCode);
        Assert.False(string.IsNullOrWhiteSpace(result.RazorpayKeyId));
        Assert.DoesNotContain("secret", result.RazorpayKeyId, StringComparison.OrdinalIgnoreCase);

        // Phase 2 must not activate the plan.
        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.FreeId, user.PlanId);
        Assert.Equal(1, razorpay.CreateCalls);
    }

    [SkippableFact]
    public async Task CreateOrder_SameIdempotencyKey_ReusesGatewayOrder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var razorpay = new FakeRazorpayClient();
        var service = ServiceFor(context, razorpay);
        var key = $"idem-{Guid.NewGuid():N}";

        var first = await service.CreateOrderAsync(
            user.Id, new CreatePaymentOrderRequest { PlanId = Plan.YakkuLoveId, IdempotencyKey = key }, "c1");
        var second = await service.CreateOrderAsync(
            user.Id, new CreatePaymentOrderRequest { PlanId = Plan.YakkuLoveId, IdempotencyKey = key }, "c2");

        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(first.RazorpayOrderId, second.RazorpayOrderId);
        Assert.Equal(1, razorpay.CreateCalls);
        Assert.Equal(1, await context.PaymentOrders.CountAsync(o => o.UserId == user.Id && o.IdempotencyKey == key));
    }

    [SkippableFact]
    public async Task CreateOrder_ActiveLite_RejectsAnotherLite()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var lite = await context.Plans.SingleAsync(p => p.Id == Plan.YakkuLiteId);
        user.ActivatePlan(lite, DateTime.UtcNow);
        await context.SaveChangesAsync();

        var service = ServiceFor(context, new FakeRazorpayClient());
        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateOrderAsync(
            user.Id,
            new CreatePaymentOrderRequest
            {
                PlanId = Plan.YakkuLiteId,
                IdempotencyKey = $"dup-lite-{Guid.NewGuid():N}"
            },
            "c"));

        Assert.Equal(ApiErrorCodes.PlanAlreadyActive, error.ErrorCode);
    }

    [SkippableFact]
    public async Task CreateOrder_ActiveLove_RejectsLiteDowngrade()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var love = await context.Plans.SingleAsync(p => p.Id == Plan.YakkuLoveId);
        user.ActivatePlan(love, DateTime.UtcNow);
        await context.SaveChangesAsync();

        var service = ServiceFor(context, new FakeRazorpayClient());
        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateOrderAsync(
            user.Id,
            new CreatePaymentOrderRequest
            {
                PlanId = Plan.YakkuLiteId,
                IdempotencyKey = $"down-{Guid.NewGuid():N}"
            },
            "c"));

        Assert.Equal(ApiErrorCodes.PlanDowngradeNotAllowed, error.ErrorCode);
    }

    [SkippableFact]
    public async Task CreateOrder_PermanentCard_RejectsDuplicatePurchase()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var poll = new Poll(user.Id, "Q?", PollContentType.Question);
        context.Polls.Add(poll);
        context.CardUnlocks.Add(CardUnlock.Create(
            user.Id, ShareContentType.Poll, poll.Id, CardUnlockSource.Purchase, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = ServiceFor(context, new FakeRazorpayClient());
        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateOrderAsync(
            user.Id,
            new CreatePaymentOrderRequest
            {
                PlanId = Plan.ThisCardId,
                ContentType = "poll",
                ContentId = poll.Id,
                IdempotencyKey = $"card-{Guid.NewGuid():N}"
            },
            "c"));

        Assert.Equal(ApiErrorCodes.CardAlreadyUnlocked, error.ErrorCode);
        Assert.Equal("You already own permanent access to this card.", error.Message);
    }

    [SkippableFact]
    public async Task CreateOrder_WhenRazorpayDisabled_Returns503()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var service = ServiceFor(context, new FakeRazorpayClient { Enabled = false }, razorpayEnabled: false);

        var error = await Assert.ThrowsAsync<AppException>(() => service.CreateOrderAsync(
            user.Id,
            new CreatePaymentOrderRequest
            {
                PlanId = Plan.YakkuLiteId,
                IdempotencyKey = $"off-{Guid.NewGuid():N}"
            },
            "c"));

        Assert.Equal(ApiErrorCodes.PaymentProviderDisabled, error.ErrorCode);
        Assert.Equal(503, error.StatusCode);
    }

    private static PaymentOrderService ServiceFor(
        YakkuDbContext context,
        IRazorpayClient razorpay,
        bool razorpayEnabled = true)
    {
        var razorpayOptions = RazorpayOptions.FromConfiguration(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Razorpay:Enabled"] = razorpayEnabled ? "true" : "false",
                    ["Razorpay:KeyId"] = "rzp_test_key",
                    ["Razorpay:KeySecret"] = "test_secret_value_not_logged",
                    ["Razorpay:WebhookSecret"] = "whsec_test"
                })
                .Build());

        return new PaymentOrderService(
            new PaymentOrderRepository(context),
            razorpay,
            new NullPaymentLogWriter(),
            razorpayOptions,
            new PaymentOptions(),
            new CreatePaymentOrderValidator(),
            TimeProvider.System,
            NullLogger<PaymentOrderService>.Instance);
    }

    private static async Task<User> SeedUserAsync(YakkuDbContext context)
    {
        var user = new User($"+9198{Random.Shared.Next(10000000, 99999999)}", $"anon-{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class FakeRazorpayClient : IRazorpayClient
    {
        public bool Enabled { get; set; } = true;
        public int CreateCalls { get; private set; }
        public bool IsEnabled => Enabled;

        public Task<RazorpayOrderResult> CreateOrderAsync(
            RazorpayCreateOrderRequest request,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(
                new RazorpayOrderResult(
                    true,
                    $"order_test_{Guid.NewGuid():N}",
                    request.AmountInPaise,
                    request.Currency,
                    "created",
                    null,
                    null));
        }

        public Task<RazorpayPaymentResult> GetPaymentAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayPaymentResult(
                    false, null, null, null, null, null, false, null, "not_implemented", "not used in order tests"));
        }

        public Task<IReadOnlyList<RazorpayPaymentResult>> GetOrderPaymentsAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<RazorpayPaymentResult>>([]);
        }

        public Task<RazorpayRefundResult> CreateRefundAsync(
            RazorpayCreateRefundRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new RazorpayRefundResult(false, null, null, null, "not_implemented", "not used in order tests"));
        }
    }
}
