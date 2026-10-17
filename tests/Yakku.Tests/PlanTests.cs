using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

public class PlanDefaultsTests
{
    [Fact]
    public void NewUser_StartsOnTheFreePlan()
    {
        var user = new User("5550000000001", "Name", "anon");

        Assert.Equal(Plan.FreeId, user.PlanId);
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
    public async Task Migration_SeedsTheThreePlansInOrder()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var plans = await context.Plans.OrderBy(p => p.SortOrder).ToListAsync();

        Assert.Equal(["free", "premium", "yakku_love"], plans.Select(p => p.Code));
        Assert.Equal([Plan.FreeId, Plan.PremiumId, Plan.YakkuLoveId], plans.Select(p => p.Id));
        Assert.Equal([0, 4900, 12900], plans.Select(p => p.PriceMinor));
        Assert.All(plans, p => Assert.Equal("INR", p.Currency));
    }

    [SkippableFact]
    public async Task SavedUser_HasAPlan_AndCanBeMovedToAnother()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new User($"557{suffix}", "Plan Tester", "plan-anon-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var loaded = await context.Users.AsNoTracking().Include(u => u.Plan).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("free", loaded.Plan.Code);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE users SET \"PlanId\" = {Plan.PremiumId} WHERE \"Id\" = {user.Id}");
        var upgraded = await context.Users.AsNoTracking().Include(u => u.Plan).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("premium", upgraded.Plan.Code);
    }
}
