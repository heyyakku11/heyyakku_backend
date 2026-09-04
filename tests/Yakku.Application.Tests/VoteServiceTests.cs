using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Application.Votes;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Interfaces;
using Yakku.Application.Votes.Services;
using Yakku.Application.Votes.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;
using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Tests.Votes;

public class VoteServiceTests
{
    [Fact]
    public async Task Cast_NormalOption_Succeeds()
    {
        var fixture = VoteFixture.Create();
        var optionId = fixture.Poll.Options.First().Id;

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            fixture.GuestId,
            new CastVoteRequest { OptionId = optionId, Reason = null });

        Assert.Single(fixture.Votes.Items);
        Assert.Equal(optionId, result.PollOptionId);
        Assert.Null(result.CustomOptionText);
        Assert.Null(result.Reason);
        Assert.Equal(fixture.Poll.Id, result.PollId);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.VoteCast);
    }

    [Fact]
    public async Task Cast_CustomOptionWithReason_Succeeds()
    {
        var fixture = VoteFixture.Create();

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            fixture.GuestId,
            new CastVoteRequest
            {
                CustomOption = "  Node.js  ",
                Reason = "  I prefer JavaScript.  "
            });

        Assert.Single(fixture.Votes.Items);
        Assert.Null(result.PollOptionId);
        Assert.Equal("Node.js", result.CustomOptionText);
        Assert.Equal("I prefer JavaScript.", result.Reason);
        Assert.DoesNotContain(
            fixture.Poll.Options,
            option => option.Text == "Node.js");
    }

    [Fact]
    public async Task Cast_Duplicate_ThrowsAlreadyVoted()
    {
        var fixture = VoteFixture.Create();
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };
        await fixture.Service.CastAsync(fixture.Poll.Id, fixture.GuestId, request);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(fixture.Poll.Id, fixture.GuestId, request));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AlreadyVoted, exception.ErrorCode);
        Assert.Equal("You have already voted in this poll.", exception.Message);
        Assert.Equal("This guest has already voted in this poll.", exception.ErrorMessage);
        Assert.Single(fixture.Votes.Items);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.VoteRejectedAlreadyVoted);
    }

    [Fact]
    public async Task Cast_SameGuestDifferentPolls_BothSucceed()
    {
        var fixture = VoteFixture.Create();
        var other = VoteFixture.CreatePoll("Second poll?");
        fixture.Polls.Items.Add(other);

        await fixture.Service.CastAsync(
            fixture.Poll.Id,
            fixture.GuestId,
            new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id });
        await fixture.Service.CastAsync(
            other.Id,
            fixture.GuestId,
            new CastVoteRequest { OptionId = other.Options.First().Id });

        Assert.Equal(2, fixture.Votes.Items.Count);
    }

    [Fact]
    public async Task Cast_DifferentGuestsSamePoll_BothSucceed()
    {
        var fixture = VoteFixture.Create();
        var otherGuest = Guid.NewGuid();
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };

        await fixture.Service.CastAsync(fixture.Poll.Id, fixture.GuestId, request);
        await fixture.Service.CastAsync(fixture.Poll.Id, otherGuest, request);

        Assert.Equal(2, fixture.Votes.Items.Count);
    }

    [Fact]
    public async Task Cast_ConcurrentDuplicate_UniqueConstraintKeepsOneVote()
    {
        var fixture = VoteFixture.Create();
        fixture.Votes.IgnoreExists = true;
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };

        await fixture.Service.CastAsync(fixture.Poll.Id, fixture.GuestId, request);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(fixture.Poll.Id, fixture.GuestId, request));

        Assert.Equal(ApiErrorCodes.AlreadyVoted, exception.ErrorCode);
        Assert.Single(fixture.Votes.Items);
    }

    [Fact]
    public async Task Cast_MissingPoll_ThrowsNotFound()
    {
        var fixture = VoteFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                Guid.NewGuid(),
                fixture.GuestId,
                new CastVoteRequest { CustomOption = "Node.js" }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Cast_ExpiredPoll_ThrowsValidationError()
    {
        var fixture = VoteFixture.Create();
        var expired = new PollEntity(Guid.NewGuid(), "Expired?", DateTime.UtcNow.AddMinutes(-1));
        expired.AddOption("A", 0);
        expired.AddOption("B", 1);
        fixture.Polls.Items.Add(expired);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                expired.Id,
                fixture.GuestId,
                new CastVoteRequest { OptionId = expired.Options.First().Id }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    [Fact]
    public async Task Cast_OptionFromAnotherPoll_ThrowsValidationError()
    {
        var fixture = VoteFixture.Create();
        var other = VoteFixture.CreatePoll("Other?");
        fixture.Polls.Items.Add(other);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                fixture.GuestId,
                new CastVoteRequest { OptionId = other.Options.First().Id }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("optionId", exception.Field);
        Assert.Empty(fixture.Votes.Items);
    }

    [Fact]
    public async Task Cast_BothOptionAndCustom_ThrowsValidation()
    {
        var fixture = VoteFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                fixture.GuestId,
                new CastVoteRequest
                {
                    OptionId = fixture.Poll.Options.First().Id,
                    CustomOption = "Node.js"
                }));
    }

    [Fact]
    public async Task Cast_NeitherOptionNorCustom_ThrowsValidation()
    {
        var fixture = VoteFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                fixture.GuestId,
                new CastVoteRequest()));
    }

    [Fact]
    public async Task Cast_ClosedPoll_ThrowsValidationError()
    {
        var fixture = VoteFixture.Create();
        typeof(PollEntity)
            .GetProperty(nameof(PollEntity.Status))!
            .SetValue(fixture.Poll, PollStatus.Closed);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                fixture.GuestId,
                new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id }));

        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    private sealed class VoteFixture
    {
        public required VoteService Service { get; init; }
        public required FakePollRepository Polls { get; init; }
        public required FakeVoteRepository Votes { get; init; }
        public required FakeSystemLogWriter Logs { get; init; }
        public required PollEntity Poll { get; init; }
        public Guid GuestId { get; init; } = Guid.NewGuid();

        public static VoteFixture Create()
        {
            var polls = new FakePollRepository();
            var votes = new FakeVoteRepository();
            var logs = new FakeSystemLogWriter();
            var poll = CreatePoll("Which option do you prefer?");
            polls.Items.Add(poll);

            return new VoteFixture
            {
                Service = new VoteService(polls, votes, logs, new CastVoteValidator()),
                Polls = polls,
                Votes = votes,
                Logs = logs,
                Poll = poll
            };
        }

        public static PollEntity CreatePoll(string question)
        {
            var poll = new PollEntity(Guid.NewGuid(), question);
            poll.AddOption("Option A", 0);
            poll.AddOption("Option B", 1);
            return poll;
        }
    }

    private sealed class FakePollRepository : IPollRepository
    {
        public List<PollEntity> Items { get; } = [];

        public Task AddAsync(PollEntity poll, CancellationToken cancellationToken = default)
        {
            Items.Add(poll);
            return Task.CompletedTask;
        }

        public Task<PollEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(poll => poll.Id == id));
        }

        public Task<IReadOnlyList<PollEntity>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PollEntity> page = Items
                .Where(poll => poll.CreatorId == userId)
                .ToList();
            return Task.FromResult(page);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeVoteRepository : IVoteRepository
    {
        public List<Vote> Items { get; } = [];
        public bool IgnoreExists { get; set; }

        public Task AddAsync(Vote vote, CancellationToken cancellationToken = default)
        {
            Items.Add(vote);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid guestId, Guid pollId, CancellationToken cancellationToken = default)
        {
            if (IgnoreExists)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(Items.Any(vote => vote.GuestId == guestId && vote.PollId == pollId));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var duplicates = Items
                .GroupBy(vote => (vote.GuestId, vote.PollId))
                .Where(group => group.Count() > 1)
                .ToList();

            if (duplicates.Count == 0)
            {
                return Task.CompletedTask;
            }

            foreach (var extra in duplicates.SelectMany(group => group.Skip(1)).ToList())
            {
                Items.Remove(extra);
            }

            throw VoteExceptions.AlreadyVoted();
        }
    }
}
