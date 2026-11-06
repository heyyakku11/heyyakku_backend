using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>The card and permission state returned with a sign-in. Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class LoginSnapshotTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public LoginSnapshotTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<User> AddUserAsync(Yakku.Infrastructure.Persistence.YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"560{sequence:D10}", $"snapshot-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static LoginSnapshotReader ReaderFor(Yakku.Infrastructure.Persistence.YakkuDbContext context) =>
        new(context, new PersonalityRepository(context));

    [SkippableFact]
    public async Task ANewUser_GetsTheirDraftCardCode_AndUnknownPermissionsWithoutADevice()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var snapshot = await ReaderFor(context).GetAsync(user.Id, null);

        Assert.Equal(user.PersonalityCard.ShareCode, snapshot.CardShareCode);
        Assert.Equal("Draft", snapshot.CardStatus);
        Assert.Equal("Unknown", snapshot.NotificationPermission);
        Assert.False(snapshot.IsPushAllowed);
    }

    [SkippableFact]
    public async Task AUserWithoutACard_GetsOneCreated()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM personality_cards WHERE user_id = {user.Id}");

        var snapshot = await ReaderFor(context).GetAsync(user.Id, "unknown-installation");

        Assert.Matches("^[0-9a-f]{6}$", snapshot.CardShareCode);
        Assert.Equal(1, await context.PersonalityCards.CountAsync(c => c.UserId == user.Id));
        Assert.Equal("Unknown", snapshot.NotificationPermission);
    }
}
