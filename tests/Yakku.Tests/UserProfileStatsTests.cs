using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Sharing;
using Yakku.Application.Streaks;
using Yakku.Application.Streaks.Services;
using Yakku.Application.Users.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>The extra profile fields: cards made, votes cast and the streak summary. Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UserProfileStatsTests
{
    private static int _sequence;
    private static readonly DateTime Today = new(2032, 5, 10, 9, 0, 0, DateTimeKind.Utc);
    private readonly PostgresFixture _fixture;

    public UserProfileStatsTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ANewUser_HasZeroCountsAndNoStreak()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var profile = await NewUserService(context).GetMeAsync(user.Id);

        Assert.Equal(0, profile!.CardsMade);
        Assert.Equal(0, profile.VotesCast);
        Assert.Equal(0, profile.Streak.CurrentStreak);
        Assert.Equal(0, profile.Streak.LongestStreak);
    }

    [SkippableFact]
    public async Task CardsMade_CountsPollsAndChecks_ButNotDrafts()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await AddPollAsync(context, user.Id);
        await AddPollAsync(context, user.Id);
        await AddPollAsync(context, user.Id, publish: false); // a draft
        await MakeCheckAsync(context, user);

        var profile = await NewUserService(context).GetMeAsync(user.Id);

        Assert.Equal(3, profile!.CardsMade);
        Assert.Equal(0, profile.VotesCast); // the owner's own answer on the check is not a cast vote
    }

    [SkippableFact]
    public async Task VotesCast_CountsOnlyAsASignedInUserOnOthersCards()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var me = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var mine = await AddPollAsync(context, me.Id);
        var theirs1 = await AddPollAsync(context, other.Id);
        var theirs2 = await AddPollAsync(context, other.Id);
        var theirs3 = await AddPollAsync(context, other.Id);
        var theirCheck = await MakeCheckAsync(context, other);

        context.PollVotes.Add(PollVote.ForOption(me.Id, mine.Id, mine.Options.First().Id, null, VoterRole.User)); // own poll
        context.PollVotes.Add(PollVote.ForOption(me.Id, theirs1.Id, theirs1.Options.First().Id, null, VoterRole.User));
        context.PollVotes.Add(PollVote.ForOption(me.Id, theirs2.Id, theirs2.Options.First().Id, null, VoterRole.User));
        context.PollVotes.Add(PollVote.ForOption(other.Id, mine.Id, mine.Options.First().Id, null, VoterRole.User)); // someone else's vote
        context.PollVotes.Add(PollVote.ForOption(me.Id, theirs3.Id, theirs3.Options.First().Id, null, VoterRole.Guest)); // a guest row that reuses the id
        await context.SaveChangesAsync();
        var checkOption = (await context.UserChecks.AsNoTracking().Include(c => c.Check).ThenInclude(c => c.Options)
            .SingleAsync(c => c.Id == theirCheck.Id)).Check.Options.First();
        context.UserCheckVotes.Add(UserCheckVote.ForOption(theirCheck.Id, theirCheck.CheckId, checkOption.Id, me.Id, null));
        await context.SaveChangesAsync();

        var profile = await NewUserService(context).GetMeAsync(me.Id);

        Assert.Equal(3, profile!.VotesCast); // two polls and one check, all by others; own poll and guest row excluded
        Assert.Equal(1, profile.CardsMade);
    }

    [SkippableFact]
    public async Task TheStreakSummary_ShowsTheEffectiveCurrentStreakAndTheRecord()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context, Today);
        foreach (var offset in new[] { -6, -5, -4, -3 }) // a run of 4 that ended 3 days ago
        {
            await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Today.AddDays(offset));
        }

        var expired = await NewUserService(context).GetMeAsync(user.Id);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Today);
        var restarted = await NewUserService(context).GetMeAsync(user.Id);

        Assert.Equal((0, 4), (expired!.Streak.CurrentStreak, expired.Streak.LongestStreak));
        Assert.Equal((1, 4), (restarted!.Streak.CurrentStreak, restarted.Streak.LongestStreak));
    }

    private static UserService NewUserService(YakkuDbContext context)
    {
        return new UserService(
            new UserRepository(context),
            new PollRepository(context),
            new VoteRepository(context),
            new UserStatsRepository(context),
            NewStreaks(context, Today));
    }

    private static StreakService NewStreaks(YakkuDbContext context, DateTime now)
    {
        return new StreakService(
            new StreakRepository(context), new TestTimeProvider(now), NullLogger<StreakService>.Instance);
    }

    private static async Task<Poll> AddPollAsync(YakkuDbContext context, Guid creatorId, bool publish = true)
    {
        var poll = new Poll(creatorId, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        if (publish)
        {
            poll.MarkPublished();
        }

        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        return poll;
    }

    private static async Task<UserCheckResponse> MakeCheckAsync(YakkuDbContext context, User owner)
    {
        var service = new CheckService(new CheckRepository(context), new CastCheckVoteValidator());
        var check = (await service.GetChecksAsync(owner.Id)).First();
        return await service.MakeAsync(
            owner.Id,
            new MakeCheckRequest { CheckId = check.Id, CheckOptionId = check.Options[0].Id });
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"561{sequence:D10}{Random.Shared.Next(10, 99)}", $"Stats {sequence}", $"stats-anon-{sequence}-{Guid.NewGuid():N}"[..40]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
