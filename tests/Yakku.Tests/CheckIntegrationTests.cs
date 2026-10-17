using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Yakku.Application.Checks;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

public class CheckWeekTests
{
    [Fact]
    public void StartOf_IsTheMondayOfTheWeek()
    {
        var sunday = new DateTime(2026, 10, 18, 23, 59, 0, DateTimeKind.Utc);
        var monday = new DateTime(2026, 10, 12, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 10, 12), CheckWeek.StartOf(sunday));
        Assert.Equal(new DateOnly(2026, 10, 12), CheckWeek.StartOf(monday));
        Assert.Equal(new DateOnly(2026, 10, 19), CheckWeek.StartOf(new DateOnly(2026, 10, 21)));
    }

    [Fact]
    public void SaveCheckValidator_EnforcesOptionsAndColor()
    {
        var validator = new SaveCheckValidator();
        var valid = NewRequest(CheckWeek.Current());

        Assert.True(validator.Validate(valid).IsValid);

        valid.Options = ["Only one"];
        Assert.False(validator.Validate(valid).IsValid);

        valid.Options = ["A", "a"];
        Assert.False(validator.Validate(valid).IsValid);

        valid.Options = ["A", "B"];
        valid.BackgroundColor = "pink";
        Assert.False(validator.Validate(valid).IsValid);
    }

    internal static SaveCheckRequest NewRequest(DateOnly week, string title = "Test Check") => new()
    {
        WeekStart = week,
        Title = title,
        Question = "Is this a test?",
        BackgroundColor = "#ABCDEF",
        Sticker = "Test",
        Options = ["Yes", "No"]
    };
}

[Collection(PostgresCollection.Name)]
public class CheckIntegrationTests
{
    private static int _userSequence = 5000;
    private readonly PostgresFixture _fixture;

    public CheckIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Migration_SeedsTheInitialChecks_LiveFromTheMigrationWeek()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var seeded = await context.Checks.Include(c => c.Options)
            .Where(c => c.Title == "Mirror Check")
            .ToListAsync();

        Assert.NotEmpty(seeded);
        Assert.Equal(["Genuinely like you", "Like having you around", "Both"],
            seeded[0].Options.OrderBy(o => o.SortOrder).Select(o => o.Text));
        Assert.True(seeded[0].LiveWeekStart <= CheckWeek.Current());
    }

    [SkippableFact]
    public async Task Users_SeeOnlyLiveChecks_EarliestFirst_AndUpcomingWeeksAppearLater()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, admin) = NewServices(context);
        var user = await AddUserAsync(context);
        var tag = Guid.NewGuid().ToString("N")[..8];
        var thisWeek = CheckWeek.Current();

        var past = await admin.CreateAsync(CheckWeekTests.NewRequest(thisWeek.AddDays(-14), $"past-{tag}"));
        var current1 = await admin.CreateAsync(CheckWeekTests.NewRequest(thisWeek.AddDays(2), $"cur1-{tag}")); // snaps to Monday
        var current2 = await admin.CreateAsync(CheckWeekTests.NewRequest(thisWeek, $"cur2-{tag}"));
        var next = await admin.CreateAsync(CheckWeekTests.NewRequest(thisWeek.AddDays(7), $"next-{tag}"));

        Assert.Equal(thisWeek, current1.WeekStart);
        Assert.Equal("upcoming", next.Status);

        var list = await service.GetChecksAsync(user.Id);
        var ours = list.Where(c => c.Title.EndsWith(tag)).ToList();

        Assert.Equal([current1.Id, current2.Id], ours.Select(c => c.Id));
        Assert.DoesNotContain(list, c => c.Id == next.Id);
        Assert.DoesNotContain(list, c => c.Id == past.Id);
        Assert.All(list, c => Assert.Equal(thisWeek, c.WeekStart));

        var currentOnly = list.Where(c => c.WeekStart == thisWeek).ToList();
        Assert.Contains(currentOnly, c => c.Id == current1.Id);
        Assert.DoesNotContain(currentOnly, c => c.Id == past.Id);

        var detail = await service.GetByIdAsync(current1.Id, user.Id);
        Assert.Equal($"cur1-{tag}", detail.Title);
        Assert.Equal(2, detail.Options.Count);
        Assert.False(detail.Used);
        Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(() => service.GetByIdAsync(next.Id, null))).StatusCode);

        var notYetLive = await Assert.ThrowsAsync<AppException>(() => service.MakeAsync(next.Id, user.Id, Answer()));
        Assert.Equal(404, notYetLive.StatusCode);

        // Rescheduling the upcoming check into this week makes it live.
        await admin.UpdateAsync(next.Id, CheckWeekTests.NewRequest(thisWeek, $"next-{tag}"));
        Assert.Contains(await service.GetChecksAsync(user.Id), c => c.Id == next.Id);
    }

    [SkippableFact]
    public async Task Admin_CannotEditContentOrDeleteAUsedCheck_ButCanDeactivateIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, admin) = NewServices(context);
        var user = await AddUserAsync(context);
        var week = CheckWeek.Current();
        var created = await admin.CreateAsync(CheckWeekTests.NewRequest(week, "used-check"));
        await service.MakeAsync(created.Id, user.Id, Answer());

        var edit = CheckWeekTests.NewRequest(week, "renamed");
        var edited = await Assert.ThrowsAsync<AppException>(() => admin.UpdateAsync(created.Id, edit));
        var deleted = await Assert.ThrowsAsync<AppException>(() => admin.DeleteAsync(created.Id));
        Assert.Equal(409, edited.StatusCode);
        Assert.Equal(409, deleted.StatusCode);

        var deactivate = CheckWeekTests.NewRequest(week, "used-check");
        deactivate.IsActive = false;
        var result = await admin.UpdateAsync(created.Id, deactivate);
        Assert.False(result.IsActive);
        Assert.DoesNotContain(await service.GetChecksAsync(user.Id), c => c.Id == created.Id);
    }

    [SkippableFact]
    public async Task Admin_CanEditAndDeleteAnUnusedCheck()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (_, admin) = NewServices(context);
        var week = CheckWeek.Current().AddDays(21);
        var created = await admin.CreateAsync(CheckWeekTests.NewRequest(week, "draft"));

        var request = CheckWeekTests.NewRequest(week, "draft v2");
        request.Options = ["Yes", "No", "Maybe"];
        var updated = await admin.UpdateAsync(created.Id, request);
        Assert.Equal("draft v2", updated.Title);
        Assert.Equal(["Yes", "No", "Maybe"], updated.Options.Select(o => o.Text));

        await admin.DeleteAsync(created.Id);
        Assert.Empty(await admin.ListAsync(week));
    }

    [SkippableFact]
    public async Task Make_IsOncePerUser_AndListShowsUsed()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var user = await AddUserAsync(context);
        var other = await AddUserAsync(context);

        var before = await service.GetChecksAsync(user.Id);
        var target = before.Last();
        Assert.All(before, c => Assert.False(c.Used));

        var made = await service.MakeAsync(target.Id, user.Id, Answer());
        var duplicate = await Assert.ThrowsAsync<AppException>(() => service.MakeAsync(target.Id, user.Id, Answer()));
        var otherUser = await service.MakeAsync(target.Id, other.Id, Answer());

        Assert.Equal(ApiErrorCodes.AlreadyUsed, duplicate.ErrorCode);
        Assert.NotEqual(made.ShareCode, otherUser.ShareCode);
        var after = await service.GetChecksAsync(user.Id);
        Assert.True(after.Single(c => c.Id == target.Id).Used);
        Assert.Equal(before.Count - 1, after.Count(c => !c.Used));
        Assert.Single(await service.GetMineAsync(user.Id));
    }

    [SkippableFact]
    public async Task Vote_OncePerVoter_NotOwner_AndResultsVisibility()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var check = (await service.GetChecksAsync(owner.Id)).First();
        var made = await service.MakeAsync(check.Id, owner.Id, Answer());
        var optionId = check.Options[0].Id;

        var ownerVote = await Assert.ThrowsAsync<AppException>(() =>
            service.VoteAsync(made.ShareCode, owner.Id, new CastCheckVoteRequest { CheckOptionId = optionId }));
        Assert.Equal(403, ownerVote.StatusCode);

        var hidden = await service.GetSharedAsync(made.ShareCode, voter.Id);
        Assert.Null(hidden.TotalVotes);

        await service.VoteAsync(made.ShareCode, voter.Id, new CastCheckVoteRequest { CheckOptionId = optionId, Reason = "Yep" });
        var twice = await Assert.ThrowsAsync<AppException>(() =>
            service.VoteAsync(made.ShareCode, voter.Id, new CastCheckVoteRequest { CheckOptionId = optionId }));
        Assert.Equal(ApiErrorCodes.AlreadyVoted, twice.ErrorCode);

        var results = await service.GetMineByIdAsync(made.Id, owner.Id);
        Assert.Equal(2, results.TotalVotes);
        Assert.Equal(1, results.CustomAnswerVotes);
        Assert.Equal("my answer", results.MyCustomAnswer);
        Assert.Equal(1, results.Options.Single(o => o.Id == optionId).Votes);
    }

    [SkippableFact]
    public async Task Vote_RejectsAnOptionFromAnotherCheck()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var checks = await service.GetChecksAsync(owner.Id);
        var made = await service.MakeAsync(checks[0].Id, owner.Id, Answer());

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            service.VoteAsync(made.ShareCode, voter.Id, new CastCheckVoteRequest { CheckOptionId = checks[1].Options[0].Id }));

        Assert.Equal(400, exception.StatusCode);
    }

    [SkippableFact]
    public async Task Make_RecordsTheMakersOwnAnswer_AsAnOptionOrACustomAnswer()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var user = await AddUserAsync(context);
        var checks = await service.GetChecksAsync(user.Id);

        var picked = await service.MakeAsync(
            checks[0].Id,
            user.Id,
            new MakeCheckRequest { CheckId = checks[0].Id, CheckOptionId = checks[0].Options[1].Id, Reason = "Because" });
        var written = await service.MakeAsync(checks[1].Id, user.Id, new MakeCheckRequest { CustomAnswer = "  Depends on the day  " });

        Assert.Equal(checks[0].Options[1].Id, picked.MyVoteOptionId);
        Assert.Equal(1, picked.Options.Single(o => o.Id == checks[0].Options[1].Id).Votes);
        Assert.Null(picked.MyCustomAnswer);
        Assert.Null(written.MyVoteOptionId);
        Assert.Equal("Depends on the day", written.MyCustomAnswer);
        Assert.Equal(1, written.CustomAnswerVotes);
    }

    [SkippableFact]
    public async Task Make_RejectsABadAnswer_AndDoesNotUseTheCheck()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var user = await AddUserAsync(context);
        var checks = await service.GetChecksAsync(user.Id);
        var target = checks[0];

        await Assert.ThrowsAsync<ValidationException>(() => service.MakeAsync(target.Id, user.Id, new MakeCheckRequest()));
        await Assert.ThrowsAsync<ValidationException>(() => service.MakeAsync(
            target.Id,
            user.Id,
            new MakeCheckRequest { CheckOptionId = target.Options[0].Id, CustomAnswer = "both" }));
        var foreign = await Assert.ThrowsAsync<AppException>(() => service.MakeAsync(
            target.Id,
            user.Id,
            new MakeCheckRequest { CheckOptionId = checks[1].Options[0].Id }));
        var mismatch = await Assert.ThrowsAsync<AppException>(() => service.MakeAsync(
            target.Id,
            user.Id,
            new MakeCheckRequest { CheckId = checks[1].Id, CustomAnswer = "x" }));

        Assert.Equal(400, foreign.StatusCode);
        Assert.Equal("checkId", mismatch.Field);
        Assert.False((await service.GetChecksAsync(user.Id)).Single(c => c.Id == target.Id).Used);
    }

    [SkippableFact]
    public async Task Vote_AFriendCanWriteACustomAnswer()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (service, _) = NewServices(context);
        var owner = await AddUserAsync(context);
        var friend = await AddUserAsync(context);
        var check = (await service.GetChecksAsync(owner.Id)).First();
        var made = await service.MakeAsync(check.Id, owner.Id, Answer());

        var vote = await service.VoteAsync(made.ShareCode, friend.Id, new CastCheckVoteRequest { CustomAnswer = "Neither" });

        Assert.Null(vote.CheckOptionId);
        Assert.Equal("Neither", vote.CustomAnswer);
        Assert.Equal(2, (await service.GetMineByIdAsync(made.Id, owner.Id)).CustomAnswerVotes);
    }

    private static MakeCheckRequest Answer() => new() { CustomAnswer = "my answer" };

    private static (CheckService Service, AdminCheckService Admin) NewServices(YakkuDbContext context)
    {
        var repository = new CheckRepository(context);
        return (
            new CheckService(repository, new CastCheckVoteValidator()),
            new AdminCheckService(repository, new SaveCheckValidator()));
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _userSequence);
        var user = new User($"556{sequence:D10}", $"Checker {sequence}", $"check-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
