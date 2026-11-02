using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminUsers.Services;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public class AdminUserIntegrationTests
{
    private static int _userSequence = 6000;
    private readonly PostgresFixture _fixture;

    public AdminUserIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Users_ListWithPlanPhoneAnonymousNameAndLastLogin_AndFilterByStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var service = NewService(context);
        var active = await AddUserAsync(context);
        var disabled = await AddUserAsync(context);
        await context.Users.Where(user => user.Id == disabled.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(user => user.Status, UserStatus.Disabled));

        var all = await service.ListUsersAsync(null, 1, 500);
        var onlyDisabled = await service.ListUsersAsync("DISABLED", 1, 500);
        var onlyActive = await service.ListUsersAsync("active", 1, 500);

        var row = Assert.Single(all.Items, item => item.Id == active.Id);
        Assert.Equal(active.Profile.AnonymousName, row.AnonymousName);
        Assert.Equal(active.PhoneNumber, row.PhoneNumber);
        Assert.Equal("active", row.Status);
        Assert.Equal("free", row.Plan.Code);
        Assert.NotNull(row.LastLoginAt);
        Assert.Null(row.AvatarUrl);

        Assert.Contains(all.Items, item => item.Id == disabled.Id);
        Assert.Contains(onlyDisabled.Items, item => item.Id == disabled.Id);
        Assert.DoesNotContain(onlyDisabled.Items, item => item.Id == active.Id);
        Assert.Contains(onlyActive.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(onlyActive.Items, item => item.Id == disabled.Id);
        Assert.All(onlyDisabled.Items, item => Assert.Equal("disabled", item.Status));
    }

    [SkippableFact]
    public async Task Users_Paginate_AndRejectAnUnknownStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var service = NewService(context);
        await AddUserAsync(context);
        await AddUserAsync(context);

        var first = await service.ListUsersAsync(null, 1, 1);
        var second = await service.ListUsersAsync(null, 2, 1);

        Assert.Single(first.Items);
        Assert.Single(second.Items);
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.True(first.Meta.TotalCount >= 2);

        var bad = await Assert.ThrowsAsync<AppException>(() => service.ListUsersAsync("banned", 1, 10));
        Assert.Equal(400, bad.StatusCode);
        await Assert.ThrowsAsync<AppException>(() => service.ListUsersAsync("1", 1, 10));
    }

    [SkippableFact]
    public async Task Guests_ListWithStatusExpiryAndCardVoteCount_AndFilterByStatus()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var service = NewService(context);
        var creator = await AddUserAsync(context);

        var live = new Guest(Guid.NewGuid().ToString("N"));
        var expired = new Guest(Guid.NewGuid().ToString("N"), DateTime.UtcNow.AddDays(-40));
        context.Guests.AddRange(live, expired);
        await context.SaveChangesAsync();

        var poll = new Poll(creator.Id, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        context.PollVotes.Add(PollVote.ForOption(
            live.Id, poll.Id, poll.Options.First().Id, reason: null, VoterRole.Guest));
        await context.SaveChangesAsync();

        var all = await service.ListGuestsAsync(null, 1, 500);
        var onlyActive = await service.ListGuestsAsync("active", 1, 500);
        var onlyExpired = await service.ListGuestsAsync("Expired", 1, 500);

        var liveRow = Assert.Single(all.Items, item => item.Id == live.Id);
        Assert.Equal("active", liveRow.Status);
        Assert.Equal(1, liveRow.CardVotes);
        Assert.NotNull(liveRow.LastSeenAt);
        Assert.Null(liveRow.Device);

        var expiredRow = Assert.Single(all.Items, item => item.Id == expired.Id);
        Assert.Equal("expired", expiredRow.Status);
        Assert.Equal(0, expiredRow.CardVotes);

        Assert.Contains(onlyActive.Items, item => item.Id == live.Id);
        Assert.DoesNotContain(onlyActive.Items, item => item.Id == expired.Id);
        Assert.Contains(onlyExpired.Items, item => item.Id == expired.Id);
        Assert.DoesNotContain(onlyExpired.Items, item => item.Id == live.Id);

        var bad = await Assert.ThrowsAsync<AppException>(() => service.ListGuestsAsync("disabled", 1, 10));
        Assert.Equal(400, bad.StatusCode);
    }

    private static AdminUserService NewService(YakkuDbContext context)
    {
        return new AdminUserService(new AdminUserRepository(context), TimeProvider.System);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _userSequence);
        var user = new User($"+916{sequence:D9}", $"Admin List {sequence}", $"admin-list-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
