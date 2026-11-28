using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

public class PlanDefaultsTests
{
    [Fact]
    public void NewUser_StartsOnTheFreePlan()
    {
        var user = new User("5550000000001", "anon");

        Assert.Equal(Plan.FreeId, user.PlanId);
    }

    [Fact]
    public void TopUpCodes_AreConversationLevel_AndNeverUserPlans()
    {
        Assert.True(Plan.IsConversationTopUpCode(Plan.TopUp19Code));
        Assert.True(Plan.IsConversationTopUpCode(Plan.TopUp29Code));
        Assert.False(Plan.IsConversationTopUpCode(Plan.YakkuLiteCode));
        Assert.False(Plan.IsConversationTopUpCode(Plan.ThisCardCode));

        Assert.False(Plan.IsUserPlanCode(Plan.TopUp19Code));
        Assert.False(Plan.IsUserPlanCode(Plan.TopUp29Code));
        Assert.True(Plan.IsUserPlanCode(Plan.YakkuLiteCode));
        Assert.True(Plan.IsUserPlanCode(Plan.YakkuLoveCode));
    }
}

[Collection(PostgresCollection.Name)]
public class PlanIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public PlanIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Migration_SeedsTheCatalogInOrder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var plans = await context.Plans.OrderBy(p => p.SortOrder).ToListAsync();

        Assert.Equal(
            ["free", "this_card", "yakku_lite", "yakku_love", "top_up_19", "top_up_29"],
            plans.Select(p => p.Code));
        Assert.Equal(
            [Plan.FreeId, Plan.ThisCardId, Plan.YakkuLiteId, Plan.YakkuLoveId, Plan.TopUp19Id, Plan.TopUp29Id],
            plans.Select(p => p.Id));
        Assert.All(plans, p => Assert.Equal("INR", p.Currency));
    }

    [SkippableFact]
    public async Task Migration_SeedsTheConversationTopUps_AsOneOffQuestionPacks()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var topUps = await context.Plans.AsNoTracking()
            .Where(p => p.Code == Plan.TopUp19Code || p.Code == Plan.TopUp29Code)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        Assert.Equal(new[] { "Top up 19", "Top up 29" }, topUps.Select(p => p.Name).ToArray());
        Assert.Equal(new[] { 1900, 2900 }, topUps.Select(p => p.PriceMinor).ToArray());
        Assert.Equal(new int?[] { 3, 5 }, topUps.Select(p => p.AskBackMessageLimit).ToArray());
        // They never expire and never change the user's plan.
        Assert.All(topUps, p => Assert.Null(p.DurationDays));
    }

    [SkippableFact]
    public async Task SavedUser_HasAPlan_AndCanBeMovedToAnother()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new User($"557{suffix}", "plan-anon-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loaded = await context.Users.AsNoTracking().Include(u => u.Plan).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("free", loaded.Plan.Code);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET \"PlanId\" = {Plan.YakkuLiteId} WHERE \"Id\" = {user.Id}");
        var upgraded = await context.Users.AsNoTracking().Include(u => u.Plan).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("yakku_lite", upgraded.Plan.Code);
        Assert.Equal("Yakku Lite", upgraded.Plan.Name);
    }
}
