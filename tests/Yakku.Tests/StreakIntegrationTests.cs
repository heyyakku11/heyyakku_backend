using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Yakku.API.Controllers;
using Yakku.Application.Auth.Services;
using Yakku.Application.Auth.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Services;
using Yakku.Application.Devices.Validators;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Services;
using Yakku.Application.Polls.Validators;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Services;
using Yakku.Application.Sharing.Validators;
using Yakku.Application.Streaks;
using Yakku.Application.Streaks.DTOs;
using Yakku.Application.Streaks.Interfaces;
using Yakku.Application.Streaks.Services;
using Yakku.Application.Votes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>A clock the test controls, so no test depends on the real date.</summary>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    public TestTimeProvider(DateTime utc)
    {
        _now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    public void Set(DateTime utc) => _now = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;
}

/// <summary>Streaks against real PostgreSQL: idempotency, concurrency, rollback and the business workflows. Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class StreakIntegrationTests
{
    private static int _sequence;
    private static readonly DateTime Monday = new(2031, 3, 3, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresFixture _fixture;

    public StreakIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- calculation -------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task TheFirstActivity_CreatesAStreakOfOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var result = await NewStreaks(context).RecordAsync(user.Id, StreakActivity.PollCreated, Monday);

        Assert.True(result.Credited);
        Assert.Equal(1, result.CurrentStreak);
        Assert.Equal(1, result.LongestStreak);
        Assert.Equal(DateOnly.FromDateTime(Monday), result.ActivityDate);
    }

    [SkippableFact]
    public async Task ManyActionsOnOneDay_CountOnce_AndARetryDoesNotDuplicate()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);

        var first = await streaks.RecordAsync(user.Id, StreakActivity.PollCreated, Monday);
        var second = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddHours(3));
        var retry = await streaks.RecordAsync(user.Id, StreakActivity.PollCreated, Monday);

        Assert.True(first.Credited);
        Assert.False(second.Credited);
        Assert.False(retry.Credited);
        Assert.Equal(1, retry.CurrentStreak);
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id));
        var day = await verify.UserActivityDays.SingleAsync(d => d.UserId == user.Id);
        Assert.Equal(Monday, day.FirstActivityAt); // the first activity of the day is kept
    }

    [SkippableFact]
    public async Task ConsecutiveDaysGrow_AGapRestarts_AndTheRecordIsKept()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);

        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(1));
        var third = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(2));
        Assert.Equal((3, 3), (third.CurrentStreak, third.LongestStreak));

        var afterGap = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(4)); // day 3 missed
        Assert.Equal((1, 3), (afterGap.CurrentStreak, afterGap.LongestStreak));

        var building = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(5));
        Assert.Equal((2, 3), (building.CurrentStreak, building.LongestStreak));

        await using var verify = _fixture.CreateContext();
        Assert.Equal(5, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id)); // history is preserved
    }

    [SkippableFact]
    public async Task ActivityAroundUtcMidnight_FollowsTheUtcDayPolicy()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);
        var lateMonday = new DateTime(2031, 3, 3, 23, 59, 59, DateTimeKind.Utc);
        var earlyTuesday = new DateTime(2031, 3, 4, 0, 0, 1, DateTimeKind.Utc);

        var a = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, lateMonday);
        var b = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, earlyTuesday);

        Assert.Equal(1, a.CurrentStreak);
        Assert.True(b.Credited); // two seconds apart, but different UTC days
        Assert.Equal(2, b.CurrentStreak);
    }

    [SkippableFact]
    public async Task AnOutOfOrderDay_RepairsFromHistory_AndNeverCorruptsTheSummary()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);

        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(2)); // gap: current 1
        var fill = await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(1)); // late event

        Assert.True(fill.Credited);
        Assert.Equal((3, 3), (fill.CurrentStreak, fill.LongestStreak));
        await using var verify = _fixture.CreateContext();
        var summary = await verify.UserStreaks.SingleAsync(s => s.UserId == user.Id);
        Assert.Equal(3, summary.CurrentStreak);
        Assert.Equal(DateOnly.FromDateTime(Monday.AddDays(2)), summary.LastActivityDate);

        // A very old event can neither reduce the record nor the current streak.
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(-30));
        var after = await verify.UserStreaks.AsNoTracking().SingleAsync(s => s.UserId == user.Id);
        Assert.Equal((3, 3), (after.CurrentStreak, after.LongestStreak));
    }

    [SkippableFact]
    public async Task RebuildFromHistory_RestoresTheSummary()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);
        foreach (var offset in new[] { 0, 1, 2, 3, 6, 7 }) // a run of 4, a gap, a run of 2
        {
            await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(offset));
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_streaks SET current_streak = 9, longest_streak = 9 WHERE user_id = {user.Id}");
        await streaks.RebuildAsync(user.Id);

        await using var verify = _fixture.CreateContext();
        var summary = await verify.UserStreaks.SingleAsync(s => s.UserId == user.Id);
        Assert.Equal((2, 4), (summary.CurrentStreak, summary.LongestStreak));
        Assert.Equal(DateOnly.FromDateTime(Monday.AddDays(7)), summary.LastActivityDate);
    }

    // ---- read model --------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task AUserWhoNeverActed_GetsZeroDefaults()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var streak = await NewStreaks(context, new TestTimeProvider(Monday)).GetAsync(user.Id);

        Assert.Equal(0, streak.CurrentStreak);
        Assert.Equal(0, streak.LongestStreak);
        Assert.Null(streak.LastActivityDate);
        Assert.False(streak.IsActiveToday);
        Assert.Equal(DateOnly.FromDateTime(Monday), streak.Today);
    }

    [SkippableFact]
    public async Task TheCurrentStreak_ExpiresAfterAFullMissedDay_WhileTheRecordStays()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var clock = new TestTimeProvider(Monday);
        var streaks = NewStreaks(context, clock);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(1));

        clock.Set(Monday.AddDays(1).AddHours(1));
        var activeToday = await streaks.GetAsync(user.Id);
        clock.Set(Monday.AddDays(2).AddHours(1)); // yesterday was the last activity: still alive
        var atRisk = await streaks.GetAsync(user.Id);
        clock.Set(Monday.AddDays(3).AddHours(1)); // a whole day missed
        var expired = await streaks.GetAsync(user.Id);

        Assert.Equal((2, true), (activeToday.CurrentStreak, activeToday.IsActiveToday));
        Assert.Equal((2, false), (atRisk.CurrentStreak, atRisk.IsActiveToday));
        Assert.Equal((0, false), (expired.CurrentStreak, expired.IsActiveToday));
        Assert.Equal(2, expired.LongestStreak);
        Assert.Equal(DateOnly.FromDateTime(Monday.AddDays(1)), expired.LastActivityDate);
    }

    [SkippableFact]
    public async Task History_IsBounded_AndListsOnlyTheCallersDays()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var streaks = NewStreaks(context, new TestTimeProvider(Monday.AddDays(10)));
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(5));
        await streaks.RecordAsync(other.Id, StreakActivity.VoteCast, Monday.AddDays(6));

        var history = await streaks.GetHistoryAsync(user.Id, DateOnly.FromDateTime(Monday), DateOnly.FromDateTime(Monday.AddDays(9)));
        var tooWide = await Assert.ThrowsAsync<AppException>(() => streaks.GetHistoryAsync(
            user.Id, DateOnly.FromDateTime(Monday.AddDays(-400)), DateOnly.FromDateTime(Monday)));
        var backwards = await Assert.ThrowsAsync<AppException>(() => streaks.GetHistoryAsync(
            user.Id, DateOnly.FromDateTime(Monday.AddDays(2)), DateOnly.FromDateTime(Monday)));

        Assert.Equal(
            new[] { DateOnly.FromDateTime(Monday), DateOnly.FromDateTime(Monday.AddDays(5)) },
            history.ActiveDates);
        Assert.Equal(400, tooWide.StatusCode);
        Assert.Equal(400, backwards.StatusCode);
    }

    // ---- concurrency, rollback, constraints --------------------------------------------------------------------

    [SkippableFact]
    public async Task ConcurrentRequestsOnTheSameDay_ProduceOneDay_AndExactlyOneCredit()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var user = await AddUserAsync(setup);

        var results = await RunConcurrentlyAsync(
            20, (streaks, _) => streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday));

        Assert.Equal(1, results.Count(r => r.Credited));
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id));
        var summary = await verify.UserStreaks.SingleAsync(s => s.UserId == user.Id);
        Assert.Equal((1, 1), (summary.CurrentStreak, summary.LongestStreak));
    }

    [SkippableFact]
    public async Task ConcurrentRequestsOnConsecutiveDays_EndWithACorrectSummary()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var user = await AddUserAsync(setup);

        await RunConcurrentlyAsync(
            6, (streaks, index) => streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(index)));

        await using var verify = _fixture.CreateContext();
        Assert.Equal(6, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id));
        var summary = await verify.UserStreaks.SingleAsync(s => s.UserId == user.Id);
        Assert.Equal((6, 6), (summary.CurrentStreak, summary.LongestStreak));
        Assert.Equal(DateOnly.FromDateTime(Monday.AddDays(5)), summary.LastActivityDate);
    }

    [SkippableFact]
    public async Task ARolledBackTransaction_LeavesNoDayAndNoSummary()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var streaks = NewStreaks(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new StreakRepository(context).ExecuteInTransactionAsync(
            async token =>
            {
                await streaks.RecordAsync(user.Id, StreakActivity.PollCreated, Monday, token);
                throw new InvalidOperationException("the business operation failed after the streak write");
            }));

        await using var verify = _fixture.CreateContext();
        Assert.Equal(0, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id));
        Assert.Equal(0, await verify.UserStreaks.CountAsync(s => s.UserId == user.Id));
    }

    [SkippableFact]
    public async Task TheDatabase_RefusesInvalidStreakData()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await NewStreaks(context).RecordAsync(user.Id, StreakActivity.VoteCast, Monday);

        var negative = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_streaks SET current_streak = -1 WHERE user_id = {user.Id}"));
        var longestBelowCurrent = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE user_streaks SET current_streak = 5, longest_streak = 4 WHERE user_id = {user.Id}"));
        var duplicateDay = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO user_activity_days (user_id, activity_date, first_activity_at) VALUES ({user.Id}, {DateOnly.FromDateTime(Monday)}, now())"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, negative.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, longestBelowCurrent.SqlState);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateDay.SqlState);
    }

    [SkippableFact]
    public async Task DeletingTheUser_RemovesTheirStreakData()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await NewStreaks(context).RecordAsync(user.Id, StreakActivity.VoteCast, Monday);

        await context.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync();

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == user.Id));
        Assert.False(await verify.UserStreaks.AnyAsync(s => s.UserId == user.Id));
    }

    // ---- ownership and devices ---------------------------------------------------------------------------------

    [SkippableFact]
    public async Task OneUsersActivity_NeverTouchesAnotherUsersStreak()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var alice = await AddUserAsync(context);
        var bob = await AddUserAsync(context);
        var streaks = NewStreaks(context);

        await streaks.RecordAsync(alice.Id, StreakActivity.PollCreated, Monday);
        await streaks.RecordAsync(alice.Id, StreakActivity.PollCreated, Monday.AddDays(1));

        var bobStreak = await NewStreaks(context, new TestTimeProvider(Monday.AddDays(1))).GetAsync(bob.Id);
        Assert.Equal(0, bobStreak.CurrentStreak);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == bob.Id));
    }

    [SkippableFact]
    public async Task StreaksBelongToTheAccount_NotToTheDeviceOrSession()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var sessions = NewSessionService(context);
        var phone = await sessions.CreateAsync(user.Id, NewInstallation());
        var tablet = await sessions.CreateAsync(user.Id, NewInstallation());
        var streaks = NewStreaks(context);

        // Activity "from" two devices is just the same account's activity on two days.
        await streaks.RecordAsync(user.Id, StreakActivity.PollCreated, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(1));

        // Logging out one device, logging out everywhere, session expiry and another account signing in on the
        // same installation must not reset or move anything.
        await sessions.RevokeAsync(phone.RefreshToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE user_sessions SET "ExpiresAt" = now() - interval '1 day' WHERE "UserId" = {user.Id}""");
        await sessions.RevokeAllAsync(user.Id);
        await sessions.CreateAsync(other.Id, NewInstallation());

        var streak = await NewStreaks(context, new TestTimeProvider(Monday.AddDays(1))).GetAsync(user.Id);
        Assert.Equal((2, 2), (streak.CurrentStreak, streak.LongestStreak));
        await using var verify = _fixture.CreateContext();
        Assert.Equal(2, await verify.UserActivityDays.CountAsync(d => d.UserId == user.Id));
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == other.Id));
        Assert.NotNull(tablet.RefreshToken);
    }

    // ---- business workflows ------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task CreatingAPoll_CreditsTheStreak_ButAnInvalidPollDoesNot()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var polls = NewPollService(context);

        var invalid = QuestionRequest();
        invalid.Options = [new CreatePollOptionRequest { Text = "Only one" }];
        await Assert.ThrowsAnyAsync<Exception>(() => polls.CreateAsync(invalid, user.Id));
        await using (var none = _fixture.CreateContext())
        {
            Assert.False(await none.UserActivityDays.AnyAsync(d => d.UserId == user.Id));
        }

        var created = await polls.CreateAsync(QuestionRequest(), user.Id);

        await using var verify = _fixture.CreateContext();
        var day = await verify.UserActivityDays.SingleAsync(d => d.UserId == user.Id);
        var poll = await verify.Polls.SingleAsync(p => p.Id == created.Id);
        Assert.Equal(DateOnly.FromDateTime(poll.CreatedAt), day.ActivityDate);
        Assert.Equal(1, (await verify.UserStreaks.SingleAsync(s => s.UserId == user.Id)).CurrentStreak);
    }

    [SkippableFact]
    public async Task AFailedPollCreation_LeavesNoPollAndNoStreakCredit()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        // A media upload session that does not exist makes the creation fail.
        var request = QuestionRequest("image");
        request.MediaUploadSessionId = Guid.NewGuid();

        await Assert.ThrowsAnyAsync<Exception>(() => NewPollService(context).CreateAsync(request, user.Id));

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == user.Id));
        Assert.False(await verify.Polls.AnyAsync(p => p.CreatorId == user.Id));
    }

    [SkippableFact]
    public async Task AnAcceptedVote_CreditsTheVoter_ButRejectedAndDuplicateVotesDoNot()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var voting = NewVotingService(context);
        var request = new VoteRequest { ShareCode = poll.ShareCode, OptionId = poll.Options.First().Id };

        await voting.VoteAsync(request, VoterIdentity.ForUser(voter.Id));

        await using (var credited = _fixture.CreateContext())
        {
            Assert.Equal(1, await credited.UserActivityDays.CountAsync(d => d.UserId == voter.Id));
        }

        // Forget the credit, then retry the same vote: it is a duplicate, so it must not credit again.
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM user_streaks WHERE user_id = {voter.Id}");
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM user_activity_days WHERE user_id = {voter.Id}");
        var duplicate = await Assert.ThrowsAsync<AppException>(
            () => NewVotingService(context).VoteAsync(request, VoterIdentity.ForUser(voter.Id)));
        Assert.Equal(409, duplicate.StatusCode);

        // A closed poll rejects the vote (it is no longer visible to others), and the rejected voter earns nothing.
        var closed = await AddActivePollAsync(context, owner.Id);
        var closedPoll = await context.Polls.SingleAsync(p => p.Id == closed.Id);
        Assert.True(closedPoll.TryClose());
        await context.SaveChangesAsync();
        var late = await Assert.ThrowsAsync<AppException>(() => NewVotingService(context).VoteAsync(
            new VoteRequest { ShareCode = closed.ShareCode, OptionId = closed.Options.First().Id },
            VoterIdentity.ForUser(voter.Id)));
        Assert.True(late.StatusCode is 400 or 404); // closed content is hidden from non-owners

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == voter.Id));
        Assert.False(await verify.UserStreaks.AnyAsync(s => s.UserId == voter.Id));
    }

    [SkippableFact]
    public async Task AGuestVote_NeverCreatesAStreak()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var bystander = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var guestId = Guid.NewGuid();

        await NewVotingService(context).VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, OptionId = poll.Options.First().Id },
            VoterIdentity.ForGuest(guestId));

        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.PollVotes.AnyAsync(v => v.VoterId == guestId));
        Assert.False(await verify.UserActivityDays.AnyAsync(d => d.UserId == guestId || d.UserId == bystander.Id));
        Assert.False(await verify.UserStreaks.AnyAsync(s => s.UserId == guestId));
    }

    // ---- API ---------------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task TheStreakEndpoint_ReturnsOnlyTheSignedInUsersTrustedValues()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var other = await AddUserAsync(context);
        var clock = new TestTimeProvider(Monday.AddDays(1));
        var streaks = NewStreaks(context, clock);
        await streaks.RecordAsync(user.Id, StreakActivity.PollCreated, Monday);
        await streaks.RecordAsync(user.Id, StreakActivity.VoteCast, Monday.AddDays(1));
        await streaks.RecordAsync(other.Id, StreakActivity.VoteCast, Monday.AddDays(1));
        await streaks.RecordAsync(other.Id, StreakActivity.VoteCast, Monday.AddDays(2));
        var controller = new UsersController(null!, null!, streaks)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())], "test"))
                }
            }
        };

        var ok = Assert.IsType<OkObjectResult>(await controller.GetStreak(CancellationToken.None));

        var body = Assert.IsType<ApiResponse<StreakResponse>>(ok.Value);
        Assert.Equal(2, body.Data!.CurrentStreak);
        Assert.Equal(2, body.Data.LongestStreak);
        Assert.True(body.Data.IsActiveToday);
        Assert.Equal(DateOnly.FromDateTime(Monday.AddDays(1)), body.Data.LastActivityDate);
    }

    // ---- helpers -----------------------------------------------------------------------------------------------

    private async Task<StreakCreditResult[]> RunConcurrentlyAsync(
        int count,
        Func<StreakService, int, Task<StreakCreditResult>> action)
    {
        var contexts = Enumerable.Range(0, count).Select(_ => _fixture.CreateContext()).ToList();
        try
        {
            using var gate = new ManualResetEventSlim(false);
            var tasks = contexts.Select((context, index) => Task.Run(async () =>
            {
                gate.Wait();
                return await action(NewStreaks(context), index);
            })).ToList();
            gate.Set();
            return await Task.WhenAll(tasks);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    private static StreakService NewStreaks(YakkuDbContext context, TimeProvider? time = null)
    {
        return new StreakService(
            new StreakRepository(context),
            time ?? new TestTimeProvider(Monday),
            NullLogger<StreakService>.Instance);
    }

    private static PollService NewPollService(YakkuDbContext context)
    {
        return new PollService(
            new PollRepository(context),
            new MediaUploadSessionRepository(context),
            new FakeObjectStorage(),
            MediaUploadPolicy.Default,
            new CreatePollValidator(),
            new UpdatePollValidator(),
            new RecordingAuditLogWriter(),
            NewStreaks(context));
    }

    private static VotingService NewVotingService(YakkuDbContext context)
    {
        return new VotingService(
            new VotableContentRepository(context),
            new FakeObjectStorage(),
            MediaUploadPolicy.Default,
            new VoteRequestValidator(),
            new RecordingAuditLogWriter(),
            NewStreaks(context));
    }

    private static CreatePollRequest QuestionRequest(string contentType = "question")
    {
        return new CreatePollRequest
        {
            Question = "Which presentation is better?",
            ContentType = contentType,
            Options =
            [
                new CreatePollOptionRequest { Text = "Option A", DisplayOrder = 1 },
                new CreatePollOptionRequest { Text = "Option B", DisplayOrder = 2 }
            ]
        };
    }

    private static async Task<Poll> AddActivePollAsync(YakkuDbContext context, Guid creatorId)
    {
        var poll = new Poll(creatorId, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        return poll;
    }

    private static RegisterDeviceRequest NewInstallation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        return new RegisterDeviceRequest
        {
            InstallationId = "streak-" + suffix,
            PushToken = "streak-token-" + suffix,
            Platform = "android",
            NotificationPermission = "granted"
        };
    }

    private static SessionService NewSessionService(YakkuDbContext context)
    {
        var devices = new DeviceService(
            new DeviceRepository(context),
            new UserSessionRepository(context),
            new GuestRepository(context),
            new GuestTokenHasher(),
            new RegisterDeviceValidator(),
            new UpdateDeviceValidator(),
            new RecordingAuditLogWriter());
        return new SessionService(
            new UserSessionRepository(context),
            new TokenService("streak-tests-secret-streak-tests-secret-1234567"),
            new RefreshTokenValidator(),
            new DeviceRepository(context),
            devices,
            new RecordingAuditLogWriter());
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"560{sequence:D10}{Random.Shared.Next(10, 99)}", $"Streak {sequence}", $"streak-anon-{sequence}-{Guid.NewGuid():N}"[..40]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
