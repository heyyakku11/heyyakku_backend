using Microsoft.EntityFrameworkCore;
using Yakku.Application.Checks;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Sharing;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Real-PostgreSQL checks for the unified vote path: registry, locking, uniqueness and the guest claim.</summary>
[Collection(PostgresCollection.Name)]
public sealed class UnifiedVotingIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public UnifiedVotingIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ShareCodeRegistry_ResolvesPollsAndMadeChecks_AndFollowsTheirLifetime()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var made = await MakeCheckAsync(context, owner);
        var repository = new VotableContentRepository(context);

        var pollContent = await repository.FindByShareCodeAsync(poll.ShareCode);
        var checkContent = await repository.FindByShareCodeAsync(made.ShareCode);

        Assert.Equal(ShareContentType.Poll, pollContent!.ContentType);
        Assert.Equal(poll.Id, pollContent.ContentId);
        Assert.Equal(2, pollContent.Options.Count);
        Assert.Equal(ShareContentType.Check, checkContent!.ContentType);
        Assert.Equal(made.Id, checkContent.ContentId);
        Assert.Equal(made.CheckId, checkContent.Check!.CheckId);
        Assert.Null(await repository.FindByShareCodeAsync("does-not-exist"));

        // Deleting the poll removes its registry row, so the code stops resolving.
        await context.Polls.Where(p => p.Id == poll.Id).ExecuteDeleteAsync();
        await using var verify = _fixture.CreateContext();
        Assert.Null(await new VotableContentRepository(verify).FindByShareCodeAsync(poll.ShareCode));
        Assert.False(await verify.ShareCodes.AnyAsync(code => code.Code == poll.ShareCode));
    }

    [SkippableFact]
    public async Task ACodeCannotBeRegisteredTwice_AcrossPollsAndChecks()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var other = await AddUserAsync(context);
        var made = await MakeCheckAsync(context, other);

        await using var second = _fixture.CreateContext();
        second.ShareCodes.Add(ShareCode.ForPoll(poll));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.IsType<Npgsql.PostgresException>(exception.InnerException);

        Assert.True(await context.ShareCodes.AnyAsync(code => code.Code == made.ShareCode));
    }

    [SkippableFact]
    public async Task Guests_CanVoteOnPollsAndChecks_WithoutAUsersRow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var made = await MakeCheckAsync(context, owner);
        var guest = VoterIdentity.ForGuest(Guid.NewGuid());
        var repository = new VotableContentRepository(context);

        var pollContent = (await repository.FindByShareCodeAsync(poll.ShareCode))!;
        var checkContent = (await repository.FindByShareCodeAsync(made.ShareCode))!;
        await repository.AddVoteAsync(pollContent, guest, new NewVote(pollContent.Options[0].Id, null, "because"));
        await repository.AddVoteAsync(checkContent, guest, new NewVote(null, "Maybe", null));

        await using var verify = _fixture.CreateContext();
        var pollVote = await verify.PollVotes.SingleAsync(v => v.PollId == poll.Id);
        var checkVote = await verify.UserCheckVotes.SingleAsync(v => v.UserCheckId == made.Id && v.VoterId == guest.Id);
        Assert.Equal(VoterRole.Guest, pollVote.VoterRole);
        Assert.Equal(VoterRole.Guest, checkVote.VoterRole);
        Assert.Equal("Maybe", checkVote.CustomAnswer);

        var tally = await new VotableContentRepository(verify).GetTallyAsync(pollContent);
        Assert.Equal(1, tally.TotalVotes);
        Assert.Equal(1, tally.OtherVotes);
        Assert.Equal(1, tally.WhyCount);
        var mine = await new VotableContentRepository(verify).GetVoteAsync(checkContent, guest);
        Assert.Equal("Maybe", mine!.CustomAnswer);
    }

    [SkippableFact]
    public async Task ConcurrentDuplicateVotes_StoreExactlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var owner = await AddUserAsync(setup);
        var poll = await AddActivePollAsync(setup, owner.Id);
        var content = (await new VotableContentRepository(setup).FindByShareCodeAsync(poll.ShareCode))!;
        var voter = VoterIdentity.ForGuest(Guid.NewGuid());

        async Task<Exception?> TryVoteAsync(int optionIndex)
        {
            await using var context = _fixture.CreateContext();
            try
            {
                await new VotableContentRepository(context).AddVoteAsync(
                    content, voter, new NewVote(content.Options[optionIndex].Id, null, null));
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var results = await Task.WhenAll(TryVoteAsync(0), TryVoteAsync(1), TryVoteAsync(0));

        Assert.Single(results, result => result is null);
        Assert.All(
            results.Where(result => result is not null),
            result => Assert.Equal(ApiErrorCodes.AlreadyVoted, Assert.IsType<AppException>(result).ErrorCode));
        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.PollVotes.CountAsync(v => v.PollId == poll.Id));
    }

    [SkippableFact]
    public async Task AVoteAfterTheClose_IsRejected_EvenWithAStaleSnapshot()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var stale = (await new VotableContentRepository(context).FindByShareCodeAsync(poll.ShareCode))!;
        Assert.True(stale.IsOpen);

        await using (var closer = _fixture.CreateContext())
        {
            var tracked = await closer.Polls.SingleAsync(p => p.Id == poll.Id);
            Assert.True(tracked.TryClose());
            await closer.SaveChangesAsync();
        }

        var exception = await Assert.ThrowsAsync<AppException>(() => new VotableContentRepository(context).AddVoteAsync(
            stale, VoterIdentity.ForGuest(Guid.NewGuid()), new NewVote(stale.Options[0].Id, null, null)));

        Assert.Equal(400, exception.StatusCode);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.PollVotes.AnyAsync(v => v.PollId == poll.Id));
    }

    [SkippableFact]
    public async Task ADeactivatedCheck_RejectsVotes()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var made = await MakeCheckAsync(context, owner);
        var stale = (await new VotableContentRepository(context).FindByShareCodeAsync(made.ShareCode))!;

        await using (var admin = _fixture.CreateContext())
        {
            // A throwaway check so the seeded ones are never touched by this test.
            var temp = Check.Create(CheckWeek.StartOf(DateTime.UtcNow.AddDays(-14)), "Temp", "Temp?", "#112233", "x", ["A", "B"]);
            admin.Checks.Add(temp);
            await admin.SaveChangesAsync();
            var tempMaker = await AddUserAsync(admin);
            var userCheck = UserCheck.Create(tempMaker.Id, temp.Id, ShareCodeGenerator.New());
            admin.UserChecks.Add(userCheck);
            admin.ShareCodes.Add(ShareCode.ForCheck(userCheck));
            await admin.SaveChangesAsync();

            var content = (await new VotableContentRepository(admin).FindByShareCodeAsync(userCheck.ShareCode))!;
            temp.Update(temp.LiveWeekStart, temp.Title, temp.Question, temp.BackgroundColor, temp.Sticker, isActive: false);
            await admin.SaveChangesAsync();

            var exception = await Assert.ThrowsAsync<AppException>(() => new VotableContentRepository(context).AddVoteAsync(
                content, VoterIdentity.ForGuest(Guid.NewGuid()), new NewVote(content.Options[0].Id, null, null)));
            Assert.Equal(400, exception.StatusCode);
        }

        // The untouched check still accepts votes.
        await new VotableContentRepository(context).AddVoteAsync(
            stale, VoterIdentity.ForGuest(Guid.NewGuid()), new NewVote(stale.Options[0].Id, null, null));
    }

    [SkippableFact]
    public async Task GuestVoteClaim_MovesVotesToTheUser_AndDropsOnesTheyAlreadyCast()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var user = await AddUserAsync(context);
        var guest = VoterIdentity.ForGuest(Guid.NewGuid());
        var repository = new VotableContentRepository(context);

        var pollA = await AddActivePollAsync(context, owner.Id);
        var pollB = await AddActivePollAsync(context, owner.Id);
        var made = await MakeCheckAsync(context, owner);
        var contentA = (await repository.FindByShareCodeAsync(pollA.ShareCode))!;
        var contentB = (await repository.FindByShareCodeAsync(pollB.ShareCode))!;
        var checkContent = (await repository.FindByShareCodeAsync(made.ShareCode))!;

        // The guest voted on A, B and the check; the user already voted on A (as themself).
        await repository.AddVoteAsync(contentA, guest, new NewVote(contentA.Options[0].Id, null, null));
        await repository.AddVoteAsync(contentB, guest, new NewVote(contentB.Options[0].Id, null, null));
        await repository.AddVoteAsync(checkContent, guest, new NewVote(checkContent.Options[0].Id, null, null));
        await repository.AddVoteAsync(contentA, VoterIdentity.ForUser(user.Id), new NewVote(contentA.Options[1].Id, null, null));

        var result = await new GuestVoteClaimRepository(context).ClaimAsync(guest.Id, user.Id);

        Assert.Equal(new Yakku.Application.Guests.Interfaces.GuestVoteClaimResult(Transferred: 2, Dropped: 1), result);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.PollVotes.AnyAsync(v => v.VoterId == guest.Id));
        Assert.False(await verify.UserCheckVotes.AnyAsync(v => v.VoterId == guest.Id));
        var onA = await verify.PollVotes.SingleAsync(v => v.PollId == pollA.Id && v.VoterId == user.Id);
        Assert.Equal(contentA.Options[1].Id, onA.PollOptionId);
        var onB = await verify.PollVotes.SingleAsync(v => v.PollId == pollB.Id && v.VoterId == user.Id);
        Assert.Equal(VoterRole.User, onB.VoterRole);
        Assert.Equal(VoterRole.User, (await verify.UserCheckVotes.SingleAsync(v => v.UserCheckId == made.Id && v.VoterId == user.Id)).VoterRole);

        // The user can no longer vote again on what their guest already answered.
        var again = await Assert.ThrowsAsync<AppException>(() => new VotableContentRepository(verify).AddVoteAsync(
            contentB, VoterIdentity.ForUser(user.Id), new NewVote(contentB.Options[1].Id, null, null)));
        Assert.Equal(ApiErrorCodes.AlreadyVoted, again.ErrorCode);
    }

    [SkippableFact]
    public async Task Tally_ExcludesTheOwnersVoteFromProgress_ForPollsAndChecks()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var poll = await AddActivePollAsync(context, owner.Id);
        var made = await MakeCheckAsync(context, owner); // stores the owner's own answer
        var repository = new VotableContentRepository(context);
        var pollContent = (await repository.FindByShareCodeAsync(poll.ShareCode))!;
        var checkContent = (await repository.FindByShareCodeAsync(made.ShareCode))!;

        await repository.AddVoteAsync(pollContent, VoterIdentity.ForUser(owner.Id), new NewVote(pollContent.Options[0].Id, null, null));
        await repository.AddVoteAsync(pollContent, VoterIdentity.ForGuest(Guid.NewGuid()), new NewVote(pollContent.Options[0].Id, null, "why"));
        await repository.AddVoteAsync(checkContent, VoterIdentity.ForGuest(Guid.NewGuid()), new NewVote(checkContent.Options[1].Id, null, null));

        var pollTally = await repository.GetTallyAsync(pollContent);
        var checkTally = await repository.GetTallyAsync(checkContent);

        Assert.Equal(2, pollTally.TotalVotes);
        Assert.Equal(1, pollTally.OtherVotes);
        Assert.Equal(2, pollTally.CountFor(pollContent.Options[0].Id));
        Assert.Equal(2, checkTally.TotalVotes); // the owner's answer plus the guest
        Assert.Equal(1, checkTally.OtherVotes);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"559{sequence:D10}", $"Voter {sequence}", $"unified-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>An active poll saved through the repository, so its share code registry row is written with it.</summary>
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

    /// <summary>Makes one of the live seeded checks for the user (which records their own answer and registers the code).</summary>
    private static async Task<UserCheckResponse> MakeCheckAsync(YakkuDbContext context, User owner)
    {
        var service = new CheckService(new CheckRepository(context), new CastCheckVoteValidator(), new NoOpStreakService());
        var check = (await service.GetChecksAsync(owner.Id)).First();
        return await service.MakeAsync(
            owner.Id,
            new MakeCheckRequest { CheckId = check.Id, CheckOptionId = check.Options[0].Id });
    }
}
