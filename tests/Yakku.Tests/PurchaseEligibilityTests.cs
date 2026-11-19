using Yakku.Application.Payments;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

public sealed class PurchaseEligibilityTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Free_CanBuy_Lite_And_Love()
    {
        Assert.Null(PurchaseEligibility.RejectUserPlanPurchase(null, Plan.YakkuLiteCode));
        Assert.Null(PurchaseEligibility.RejectUserPlanPurchase(null, Plan.YakkuLoveCode));
    }

    [Fact]
    public void ExpiredPaid_CountsAsFree()
    {
        var expired = PurchaseEligibility.ResolveEffectivePaidPlan(
            Plan.YakkuLiteId, Now.AddMinutes(-1), Now);
        Assert.Null(expired);
        Assert.Null(PurchaseEligibility.RejectUserPlanPurchase(expired, Plan.YakkuLiteCode));
    }

    [Fact]
    public void ActiveLite_RejectsAnotherLite_AllowsLove()
    {
        var lite = new PurchaseEligibility.EffectivePaidPlan(Plan.YakkuLiteId, Plan.YakkuLiteCode, Now.AddDays(3));
        Assert.Contains("Lite", PurchaseEligibility.RejectUserPlanPurchase(lite, Plan.YakkuLiteCode));
        Assert.Null(PurchaseEligibility.RejectUserPlanPurchase(lite, Plan.YakkuLoveCode));
    }

    [Fact]
    public void ActiveLove_RejectsLove_And_LiteDowngrade()
    {
        var love = new PurchaseEligibility.EffectivePaidPlan(Plan.YakkuLoveId, Plan.YakkuLoveCode, Now.AddDays(10));
        Assert.Contains("Love", PurchaseEligibility.RejectUserPlanPurchase(love, Plan.YakkuLoveCode));
        Assert.Contains("downgrade", PurchaseEligibility.RejectUserPlanPurchase(love, Plan.YakkuLiteCode)!);
    }
}
