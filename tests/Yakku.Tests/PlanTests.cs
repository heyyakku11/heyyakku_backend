using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

public class PlanDefaultsTests
{
    [Fact]
    public void NewUser_StartsOnTheFreePlan()
    {
        var user = new User("5550000000001", "anon");

        Assert.Equal(Plan.FreeCode, user.UserPlan.PlanCode);
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

        var loaded = await context.UserPlans.AsNoTracking().Include(p => p.Plan).SingleAsync(p => p.UserId == user.Id);
        Assert.Equal("free", loaded.PlanCode);
        Assert.Equal("free", loaded.Plan.Code);
        Assert.Null(loaded.PlanActivatedAt);
        Assert.Null(loaded.PlanExpiresAt);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_plans SET plan_code = {Plan.YakkuLiteCode} WHERE user_id = {user.Id}");
        var upgraded = await context.UserPlans.AsNoTracking().Include(p => p.Plan).SingleAsync(p => p.UserId == user.Id);
        Assert.Equal("yakku_lite", upgraded.PlanCode);
        Assert.Equal("Yakku Lite", upgraded.Plan.Name);
    }

    [SkippableFact]
    public async Task UserPlan_IsOneToOneWithTheUser_OverTheUserId()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new User($"558{suffix}", "plan-one-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        // The plan row is created with the account, on the free plan.
        Assert.Equal(1, await context.UserPlans.CountAsync(p => p.UserId == user.Id));

        // A second row for the same user is refused: the user id is the key.
        var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO user_plans (user_id, plan_code) VALUES ({user.Id}, {Plan.FreeCode})"));
        Assert.NotNull(duplicate);

        // The plan code has to be a real plan.
        var unknown = await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_plans SET plan_code = 'no_such_plan' WHERE user_id = {user.Id}"));
        Assert.NotNull(unknown);

        // The users table no longer holds any plan columns.
        var columns = await context.Database
            .SqlQuery<string>($"""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_name = 'users' AND column_name IN ('PlanId','PlanActivatedAt','PlanExpiresAt')""")
            .ToListAsync();
        Assert.Empty(columns);

        // Removing the user removes the plan row with it.
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM users WHERE \"Id\" = {user.Id}");
        Assert.Equal(0, await context.UserPlans.CountAsync(p => p.UserId == user.Id));
    }
}
