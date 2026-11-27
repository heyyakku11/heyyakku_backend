using static Yakku.Tests.PersonalityTestIds;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.Guests.Services;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Guest personality votes follow the guest to the account, and website sessions stay out of the cap.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GuestClaimPersonalityAndWebSessionTests
{
    private static readonly GuestTokenHasher Hasher = new();
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public GuestClaimPersonalityAndWebSessionTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Claim_MovesAGuestPersonalityVoteToTheUser_AndKeepsTheCardCounter()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var user = await AddUserAsync(context);
        var guest = await AddGuestAsync(context);
        var cardId = await CardIdAsync(context, owner.Id);
        context.PersonalityVotes.Add(new PersonalityVote(cardId, guest.Id, true, new[] { 1, 2, 3 }.Select(T)));
        await context.SaveChangesAsync();
        await SetTotalVotesAsync(context, cardId, 1);

        var result = await new GuestVoteClaimRepository(context).ClaimAsync(guest.Id, user.Id);

        Assert.Equal(1, result.PersonalityTransferred);
        Assert.Equal(0, result.PersonalityDropped);
        await using var verify = _fixture.CreateContext();
        var vote = await verify.PersonalityVotes.Include(v => v.Traits).SingleAsync(v => v.PersonalityCardId == cardId);
        Assert.Equal(user.Id, vote.VoterUserId);
        Assert.Null(vote.VoterGuestId);
        Assert.Equal(3, vote.Traits.Count);
        Assert.Equal(1, await verify.PersonalityCards.Where(c => c.Id == cardId).Select(c => c.TotalVotes).SingleAsync());
    }

    [SkippableFact]
    public async Task Claim_DropsAGuestPersonalityVote_WhenTheUserAlreadyVotedOnThatCard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var user = await AddUserAsync(context);
        var guest = await AddGuestAsync(context);
        var cardId = await CardIdAsync(context, owner.Id);
        context.PersonalityVotes.Add(new PersonalityVote(cardId, user.Id, false, new[] { 1, 2, 3 }.Select(T)));
        context.PersonalityVotes.Add(new PersonalityVote(cardId, guest.Id, true, new[] { 4, 5, 6 }.Select(T)));
        await context.SaveChangesAsync();
        await SetTotalVotesAsync(context, cardId, 2);

        var result = await new GuestVoteClaimRepository(context).ClaimAsync(guest.Id, user.Id);

        Assert.Equal(0, result.PersonalityTransferred);
        Assert.Equal(1, result.PersonalityDropped);
        await using var verify = _fixture.CreateContext();
        var votes = await verify.PersonalityVotes.Where(v => v.PersonalityCardId == cardId).ToListAsync();
        Assert.Single(votes);
        Assert.Equal(user.Id, votes[0].VoterUserId);
        Assert.Equal(1, await verify.PersonalityCards.Where(c => c.Id == cardId).Select(c => c.TotalVotes).SingleAsync());
    }

    [SkippableFact]
    public async Task Claim_LeavesOtherGuestsPersonalityVotesAlone()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var user = await AddUserAsync(context);
        var guest = await AddGuestAsync(context);
        var other = await AddGuestAsync(context);
        var cardId = await CardIdAsync(context, owner.Id);
        context.PersonalityVotes.Add(new PersonalityVote(cardId, other.Id, true, new[] { 1, 2, 3 }.Select(T)));
        await context.SaveChangesAsync();

        await new GuestVoteClaimRepository(context).ClaimAsync(guest.Id, user.Id);

        await using var verify = _fixture.CreateContext();
        Assert.Equal(other.Id, (await verify.PersonalityVotes.SingleAsync(v => v.PersonalityCardId == cardId)).VoterGuestId);
    }

    [SkippableFact]
    public async Task WebSessions_AreNotCountedAgainstTheLiveSessionCap()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var app = new UserSession(user.Id, "app-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(30));
        var web = new UserSession(user.Id, "web-" + Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddMinutes(15), isWeb: true);
        context.UserSessions.AddRange(app, web);
        await context.SaveChangesAsync();
        var repository = new UserSessionRepository(context);

        var counted = await repository.ListLiveByUserIdAsync(user.Id, DateTime.UtcNow);

        Assert.Equal(app.Id, Assert.Single(counted).Id);
        // The web session is still a real, live session: the live-session filter accepts it until it expires.
        Assert.True(await repository.IsLiveAsync(web.Id, user.Id, DateTime.UtcNow));
        Assert.False(await repository.IsLiveAsync(web.Id, user.Id, DateTime.UtcNow.AddMinutes(16)));
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"558{sequence:D10}", $"web-claim-anon-{Guid.NewGuid():N}"[..40]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Guest> AddGuestAsync(YakkuDbContext context)
    {
        var guest = new Guest(Hasher.Hash("token-" + Guid.NewGuid().ToString("N")));
        context.Guests.Add(guest);
        await context.SaveChangesAsync();
        return guest;
    }

    private static async Task<Guid> CardIdAsync(YakkuDbContext context, Guid ownerId)
    {
        return await context.PersonalityCards.AsNoTracking().Where(c => c.UserId == ownerId).Select(c => c.Id).SingleAsync();
    }

    private static Task<int> SetTotalVotesAsync(YakkuDbContext context, Guid cardId, int total)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"UPDATE personality_cards SET total_votes = {total} WHERE id = {cardId}");
    }
}
