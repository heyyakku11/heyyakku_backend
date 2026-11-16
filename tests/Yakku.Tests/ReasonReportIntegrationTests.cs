using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminReasonReports.Services;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>
/// Real-PostgreSQL checks for reported reasons: a report flags the reason, an admin's approval blocks it
/// (counted, never sent) and a rejection clears the flag.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReasonReportIntegrationTests
{
    private static int _sequence = 8000;
    private readonly PostgresFixture _fixture;

    public ReasonReportIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ReportedReason_StaysVisibleAndCounted_FlaggedAsPending()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);

        var report = await ReasonServiceFor(context).ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = " Mean or bullying " });

        Assert.Equal("pending", report.Status);
        Assert.Equal(3, report.OptionWhyCount);
        var stored = await context.ReasonReports.SingleAsync(item => item.VoteId == scene.Reported.Id);
        Assert.Equal("Mean or bullying", stored.ReporterReason);
        Assert.Equal(ReasonReportStatus.Pending, stored.Status);

        var list = await ReasonServiceFor(context).GetPollOptionReasonsAsync(scene.Poll.Id, scene.Owner.Id, scene.OptionId);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(0, list.BlockedCount);
        Assert.Equal("pending", list.Items.Single(item => item.Id == scene.Reported.Id).ReportStatus);
        Assert.All(list.Items.Where(item => item.Id != scene.Reported.Id), item => Assert.Equal("none", item.ReportStatus));
    }

    [SkippableFact]
    public async Task ReportingAGuestsReason_IsAllowed_AndReportingTwiceChangesNothing()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ReasonServiceFor(context);

        var first = await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.GuestVote.Id, new ReportReasonRequest { Reason = "spam reason" });
        var second = await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.GuestVote.Id, new ReportReasonRequest { Reason = "hate reason" });

        Assert.Equal("pending", first.Status);
        Assert.Equal("pending", second.Status);
        var stored = await context.ReasonReports.SingleAsync(item => item.VoteId == scene.GuestVote.Id);
        Assert.Equal("spam reason", stored.ReporterReason);
    }

    [SkippableFact]
    public async Task Report_NeedsAReasonWithinTheLimit_AndAnOwner()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ReasonServiceFor(context);

        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, null))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "   " }))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id,
            new ReportReasonRequest { Reason = new string('x', ReasonReport.MaxReporterReasonLength + 1) }))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(() => service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Voters[0].Id, scene.Reported.Id, new ReportReasonRequest { Reason = "other reason" }))).StatusCode);
        Assert.Equal(0, await context.ReasonReports.CountAsync(item => item.VoteId == scene.Reported.Id));
    }

    [SkippableFact]
    public async Task ApprovedReport_RemovesTheReasonFromTheApi_AndCountsItAsBlocked()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ReasonServiceFor(context);
        await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "sexual reason" });

        var admin = AdminServiceFor(context);
        var pendingList = await admin.ListAsync("pending", 1, 50);
        var reportId = Assert.Single(pendingList.Items, item => item.VoteId == scene.Reported.Id).Id;
        await admin.ApproveAsync(reportId);

        var list = await service.GetPollOptionReasonsAsync(scene.Poll.Id, scene.Owner.Id, scene.OptionId);
        Assert.Equal(2, list.Items.Count);
        Assert.DoesNotContain(list.Items, item => item.Id == scene.Reported.Id);
        Assert.DoesNotContain(list.Items, item => item.Text == scene.ReportedText);
        Assert.Equal(1, list.BlockedCount);

        var again = await Assert.ThrowsAsync<AppException>(() => admin.ApproveAsync(reportId));
        Assert.Equal(409, again.StatusCode);
        var reportAgain = await Assert.ThrowsAsync<AppException>(() => service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "sexual reason" }));
        Assert.Equal(404, reportAgain.StatusCode);
        Assert.Equal(ReasonReportStatus.Approved, (await context.ReasonReports.AsNoTracking().SingleAsync(item => item.Id == reportId)).Status);
    }

    [SkippableFact]
    public async Task RejectedReport_KeepsTheReason_ClearsTheFlag_AndCannotBeReportedAgain()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ReasonServiceFor(context);
        await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "spam reason" });

        var admin = AdminServiceFor(context);
        var reportId = (await context.ReasonReports.SingleAsync(item => item.VoteId == scene.Reported.Id)).Id;
        await admin.RejectAsync(reportId);

        var list = await service.GetPollOptionReasonsAsync(scene.Poll.Id, scene.Owner.Id, scene.OptionId);
        Assert.Equal(3, list.Items.Count);
        Assert.Equal(0, list.BlockedCount);
        Assert.Equal("rejected", list.Items.Single(item => item.Id == scene.Reported.Id).ReportStatus);

        var again = await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "spam reason" });
        Assert.Equal("rejected", again.Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => admin.ApproveAsync(reportId))).StatusCode);
    }

    [SkippableFact]
    public async Task ApprovedReportOnACustomAnswersReason_KeepsTheAnswer_AndCountsItBlocked()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ReasonServiceFor(context);

        var before = await service.GetPollCustomAnswersAsync(scene.Poll.Id, scene.Owner.Id);
        var answer = Assert.Single(before.Items);
        Assert.Equal("Their own idea", answer.Answer);
        Assert.Equal("none", answer.ReasonReportStatus);

        await service.ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.CustomVote.Id, new ReportReasonRequest { Reason = "hate reason" });
        var flagged = Assert.Single((await service.GetPollCustomAnswersAsync(scene.Poll.Id, scene.Owner.Id)).Items);
        Assert.Equal("pending", flagged.ReasonReportStatus);
        Assert.NotNull(flagged.Reason);

        var reportId = (await context.ReasonReports.SingleAsync(item => item.VoteId == scene.CustomVote.Id)).Id;
        await AdminServiceFor(context).ApproveAsync(reportId);

        var after = await service.GetPollCustomAnswersAsync(scene.Poll.Id, scene.Owner.Id);
        var kept = Assert.Single(after.Items);
        Assert.Equal("Their own idea", kept.Answer);
        Assert.Null(kept.Reason);
        Assert.Equal("none", kept.ReasonReportStatus);
        Assert.Equal(1, after.BlockedCount);

        // Blocked custom-answer reasons are not counted against the option.
        var option = await service.GetPollOptionReasonsAsync(scene.Poll.Id, scene.Owner.Id, scene.OptionId);
        Assert.Equal(0, option.BlockedCount);
    }

    [SkippableFact]
    public async Task AdminList_FiltersByStatus_AndRejectsAnUnknownOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        await ReasonServiceFor(context).ReportPollReasonAsync(
            scene.Poll.Id, scene.Owner.Id, scene.Reported.Id, new ReportReasonRequest { Reason = "Rude to someone" });
        var admin = AdminServiceFor(context);

        var pending = await admin.ListAsync("pending", 1, 50);
        var row = Assert.Single(pending.Items, item => item.VoteId == scene.Reported.Id);
        Assert.Equal("poll", row.ContentType);
        Assert.Equal(scene.ReportedText, row.ReasonText);
        Assert.Equal("Rude to someone", row.ReporterReason);
        Assert.Equal(scene.Owner.Profile.AnonymousName, row.ReporterAnonymousName);
        Assert.DoesNotContain(
            (await admin.ListAsync("approved", 1, 50)).Items, item => item.VoteId == scene.Reported.Id);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(() => admin.ListAsync("later", 1, 50))).StatusCode);
        Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(() => admin.ApproveAsync(Guid.NewGuid()))).StatusCode);
    }

    private static ReasonService ReasonServiceFor(YakkuDbContext context) => new(new ReasonRepository(context));

    private static AdminReasonReportService AdminServiceFor(YakkuDbContext context) =>
        new(new AdminReasonReportRepository(context), TimeProvider.System);

    private sealed record Scene(
        User Owner,
        List<User> Voters,
        Poll Poll,
        Guid OptionId,
        PollVote Reported,
        string ReportedText,
        PollVote GuestVote,
        PollVote CustomVote);

    /// <summary>
    /// A poll with enough other voters to unlock reasons: three users (and one guest) gave a reason for option A, and
    /// one more voter wrote in their own answer with a reason.
    /// </summary>
    private static async Task<Scene> SceneAsync(YakkuDbContext context)
    {
        var owner = await AddUserAsync(context);
        var voters = new List<User>();
        for (var i = 0; i < 3; i++)
        {
            voters.Add(await AddUserAsync(context));
        }

        var poll = new Poll(owner.Id, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();

        var optionId = poll.Options.First().Id;
        const string reportedText = "You are the worst";
        var reported = PollVote.ForOption(voters[0].Id, poll.Id, optionId, reportedText);
        var second = PollVote.ForOption(voters[1].Id, poll.Id, optionId, "Clearer slides");
        var guest = PollVote.ForOption(Guid.NewGuid(), poll.Id, optionId, "Guest says hi", VoterRole.Guest);
        var custom = PollVote.ForCustomAnswer(voters[2].Id, poll.Id, "Their own idea", "Because I like it");
        context.PollVotes.AddRange(reported, second, guest, custom);
        await context.SaveChangesAsync();
        return new Scene(owner, voters, poll, optionId, reported, reportedText, guest, custom);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"581{sequence:D10}", $"reportreason-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
