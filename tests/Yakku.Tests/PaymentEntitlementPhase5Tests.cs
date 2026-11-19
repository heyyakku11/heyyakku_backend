using Microsoft.EntityFrameworkCore;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Phase 5: entitlement-scoped tickets, plan upgrades, expiry, permanent this_card unlocks.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PaymentEntitlementPhase5Tests
{
    private readonly PostgresFixture _fixture;

    public PaymentEntitlementPhase5Tests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Lite_UnlocksAttachEntitlementId_AndFourthCardIsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);

        var entitlementId = await context.UserPlanEntitlements
            .Where(e => e.UserId == asker.Id && e.Status == UserPlanEntitlementStatus.Active)
            .Select(e => e.Id)
            .SingleAsync();

        var cards = new List<(Poll Poll, PollVote Vote, User Voter)>();
        for (var i = 0; i < 4; i++)
        {
            cards.Add(await AskBackEntitlementIntegrationTests.CardWithReasonAsync(context, asker.Id));
        }

        for (var i = 0; i < 3; i++)
        {
            await entitlements.UnlockAsync(asker.Id, cards[i].Vote.Id);
        }

        Assert.Equal(
            3,
            await context.CardUnlocks.CountAsync(u =>
                u.OwnerUserId == asker.Id
                && u.Source == CardUnlockSource.Plan
                && u.UserPlanEntitlementId == entitlementId));

        var denied = await Assert.ThrowsAsync<AppException>(
            () => entitlements.UnlockAsync(asker.Id, cards[3].Vote.Id));
        Assert.Equal(ApiErrorCodes.AskBackUnlockRequired, denied.ErrorCode);
    }

    [SkippableFact]
    public async Task NewLiteEntitlement_HasFreshTickets_OldUnlocksDoNotCount()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AskBackIntegrationTests.AddUserAsync(context);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);

        var cards = new List<(Poll Poll, PollVote Vote, User Voter)>();
        for (var i = 0; i < 4; i++)
        {
            cards.Add(await AskBackEntitlementIntegrationTests.CardWithReasonAsync(context, asker.Id));
        }

        for (var i = 0; i < 3; i++)
        {
            await entitlements.UnlockAsync(asker.Id, cards[i].Vote.Id);
        }

        var firstEntitlementId = await context.UserPlanEntitlements
            .Where(e => e.UserId == asker.Id && e.Status == UserPlanEntitlementStatus.Active)
            .Select(e => e.Id)
            .SingleAsync();

        await entitlements.ActivatePlanAsync(asker.Id, Plan.YakkuLiteCode);

        Assert.Equal(
            UserPlanEntitlementStatus.Superseded,
            await context.UserPlanEntitlements
                .Where(e => e.Id == firstEntitlementId)
                .Select(e => e.Status)
                .SingleAsync());

        var access = await entitlements.GetAccessAsync(asker.Id, cards[3].Vote.Id);
        Assert.Equal(3, access.TicketsLeft);

        var unlocked = await entitlements.UnlockAsync(asker.Id, cards[3].Vote.Id);
        Assert.Equal(2, unlocked.TicketsLeft);

        var secondEntitlementId = await context.UserPlanEntitlements
            .Where(e => e.UserId == asker.Id && e.Status == UserPlanEntitlementStatus.Active)
            .Select(e => e.Id)
            .SingleAsync();
        Assert.NotEqual(firstEntitlementId, secondEntitlementId);
        Assert.Equal(
            secondEntitlementId,
            await context.CardUnlocks
                .Where(u => u.ContentId == cards[3].Poll.Id)
                .Select(u => u.UserPlanEntitlementId)
                .SingleAsync());
    }

    [SkippableFact]
    public async Task Payment_LiteToLove_SupersedesLiteEntitlement()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var liteOrder = await SeedPlanOrderAsync(context, user, Plan.YakkuLiteId);
        await FulfillAsync(context, liteOrder, $"pay_lite_{Guid.NewGuid():N}");

        var liteEntitlementId = await context.UserPlanEntitlements
            .Where(e => e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active)
            .Select(e => e.Id)
            .SingleAsync();

        var loveOrder = await SeedPlanOrderAsync(context, user, Plan.YakkuLoveId);
        await FulfillAsync(context, loveOrder, $"pay_love_{Guid.NewGuid():N}");

        await context.Entry(user).ReloadAsync();
        Assert.Equal(Plan.YakkuLoveId, user.PlanId);
        Assert.Equal(
            UserPlanEntitlementStatus.Superseded,
            await context.UserPlanEntitlements.Where(e => e.Id == liteEntitlementId).Select(e => e.Status).SingleAsync());
        Assert.Equal(
            1,
            await context.UserPlanEntitlements.CountAsync(e =>
                e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active && e.PlanId == Plan.YakkuLoveId));
    }

    [SkippableFact]
    public async Task ThisCard_PermanentUnlock_SurvivesPlanExpiry()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var poll = await AskBackIntegrationTests.AddPollAsync(context, user.Id);

        var thisCard = await context.Plans.AsNoTracking().SingleAsync(p => p.Id == Plan.ThisCardId);
        var cardOrder = PaymentOrder.CreateCardUnlock(
            user.Id,
            Plan.ThisCardId,
            ShareContentType.Poll,
            poll.Id,
            thisCard.PriceMinor,
            thisCard.Currency,
            PlanPurchaseSnapshot.FromPlan(thisCard).ToJson(),
            $"idem-card-{Guid.NewGuid():N}",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(15),
            "phase5");
        cardOrder.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(cardOrder);
        await context.SaveChangesAsync();
        await FulfillAsync(context, cardOrder, $"pay_card_{Guid.NewGuid():N}");

        var liteOrder = await SeedPlanOrderAsync(context, user, Plan.YakkuLiteId);
        await FulfillAsync(context, liteOrder, $"pay_lite2_{Guid.NewGuid():N}");

        await AskBackIntegrationTests.ExpirePaidPlanAsync(context, user.Id);

        var access = await AskBackIntegrationTests.EntitlementsFor(context)
            .GetOwnedCardAccessAsync(user.Id, "poll", poll.Id);

        Assert.True(access.Unlocked);
        Assert.Equal("purchase", access.UnlockSource);
        Assert.False(access.Paused);
        Assert.True(access.PlanExpired);
        Assert.Equal(
            1,
            await context.CardUnlocks.CountAsync(u =>
                u.OwnerUserId == user.Id
                && u.ContentId == poll.Id
                && u.Source == CardUnlockSource.Purchase
                && u.PaymentOrderId == cardOrder.Id));
    }

    [SkippableFact]
    public async Task ExpiredLite_AllowsRepurchase_AndMarksEntitlementExpired()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await SeedUserAsync(context);
        var order = await SeedPlanOrderAsync(context, user, Plan.YakkuLiteId);
        await FulfillAsync(context, order, $"pay_exp_{Guid.NewGuid():N}");

        var entitlementId = await context.UserPlanEntitlements
            .Where(e => e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active)
            .Select(e => e.Id)
            .SingleAsync();

        await AskBackIntegrationTests.ExpirePaidPlanAsync(context, user.Id);

        var poll = await AskBackIntegrationTests.AddPollAsync(context, user.Id);
        var access = await AskBackIntegrationTests.EntitlementsFor(context)
            .GetOwnedCardAccessAsync(user.Id, "poll", poll.Id);
        Assert.True(access.PlanExpired);
        Assert.Equal(
            UserPlanEntitlementStatus.Expired,
            await context.UserPlanEntitlements.Where(e => e.Id == entitlementId).Select(e => e.Status).SingleAsync());

        var repurchase = await SeedPlanOrderAsync(context, user, Plan.YakkuLiteId);
        await FulfillAsync(context, repurchase, $"pay_re_{Guid.NewGuid():N}");

        Assert.Equal(
            1,
            await context.UserPlanEntitlements.CountAsync(e =>
                e.UserId == user.Id && e.Status == UserPlanEntitlementStatus.Active && e.PlanId == Plan.YakkuLiteId));
    }

    private static async Task FulfillAsync(YakkuDbContext context, PaymentOrder order, string paymentId)
    {
        var repo = new PaymentFulfillmentRepository(context);
        var service = new PaymentFulfillmentService(repo, new NullPaymentLogWriter());
        await service.FulfillCapturedPaymentAsync(
            new CapturedPaymentFulfillmentCommand(
                order.Id,
                order.RazorpayOrderId!,
                paymentId,
                order.AmountInPaise,
                order.Currency,
                "upi",
                "captured",
                DateTime.UtcNow,
                "phase5"));
    }

    private async Task<PaymentOrder> SeedPlanOrderAsync(YakkuDbContext context, User user, Guid planId)
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
            "phase5");
        order.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task<User> SeedUserAsync(YakkuDbContext context)
    {
        var user = new User($"+9196{Random.Shared.Next(10000000, 99999999)}", $"p5-{Guid.NewGuid():N}"[..12]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
