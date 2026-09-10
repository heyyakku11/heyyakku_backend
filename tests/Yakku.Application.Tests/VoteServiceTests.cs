using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.Polls;
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
            null,
            fixture.GuestId,
            new CastVoteRequest { OptionId = optionId, Reason = null });

        Assert.Single(fixture.Votes.Items);
        Assert.Equal(optionId, result.PollOptionId);
        Assert.Null(result.CustomOptionText);
        Assert.Null(result.ImageId);
        Assert.Null(result.Reason);
        Assert.Equal(fixture.Poll.Id, result.PollId);
        Assert.Equal(fixture.GuestId, fixture.Votes.Items[0].GuestId);
        Assert.Null(fixture.Votes.Items[0].UserId);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.VoteCast);
    }

    [Fact]
    public async Task Cast_AsUser_Succeeds()
    {
        var fixture = VoteFixture.Create();
        var userId = Guid.NewGuid();
        var optionId = fixture.Poll.Options.First().Id;

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            userId,
            null,
            new CastVoteRequest { OptionId = optionId });

        Assert.Single(fixture.Votes.Items);
        Assert.Equal(optionId, result.PollOptionId);
        Assert.Equal(userId, fixture.Votes.Items[0].UserId);
        Assert.Null(fixture.Votes.Items[0].GuestId);
        Assert.Contains(
            fixture.Logs.Entries,
            entry => entry.EventType == SystemLogEventTypes.VoteCast && entry.UserId == userId);
    }

    [Fact]
    public async Task Cast_AsUser_Duplicate_ThrowsAlreadyVoted()
    {
        var fixture = VoteFixture.Create();
        var userId = Guid.NewGuid();
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };

        await fixture.Service.CastAsync(fixture.Poll.Id, userId, null, request);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(fixture.Poll.Id, userId, null, request));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AlreadyVoted, exception.ErrorCode);
        Assert.Single(fixture.Votes.Items);
        Assert.Contains(
            fixture.Logs.Entries,
            entry => entry.EventType == SystemLogEventTypes.VoteRejectedAlreadyVoted);
    }

    [Fact]
    public async Task Cast_CustomOptionWithReason_Succeeds()
    {
        var fixture = VoteFixture.Create();

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            null,
            fixture.GuestId,
            new CastVoteRequest
            {
                CustomOption = "  Node.js  ",
                Reason = "  I prefer JavaScript.  "
            });

        Assert.Single(fixture.Votes.Items);
        Assert.Null(result.PollOptionId);
        Assert.Equal("Node.js", result.CustomOptionText);
        Assert.Null(result.ImageId);
        Assert.Equal("I prefer JavaScript.", result.Reason);
        Assert.DoesNotContain(
            fixture.Poll.Options,
            option => option.Text == "Node.js");
    }

    [Fact]
    public async Task Cast_ImagePoll_ExistingOption_Succeeds()
    {
        var fixture = VoteFixture.CreateImagePoll();
        var optionId = fixture.Poll.Options.First().Id;

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            null,
            fixture.GuestId,
            new CastVoteRequest { OptionId = optionId });

        Assert.Equal(optionId, result.PollOptionId);
        Assert.Null(result.ImageId);
        Assert.Null(result.CustomOptionText);
    }

    [Fact]
    public async Task Cast_ImagePoll_CustomImage_Succeeds()
    {
        var fixture = VoteFixture.CreateImagePoll();
        var customImageId = Guid.NewGuid();
        fixture.Images.Existing.Add(customImageId);

        var result = await fixture.Service.CastAsync(
            fixture.Poll.Id,
            null,
            fixture.GuestId,
            new CastVoteRequest { ImageId = customImageId, Reason = "My outfit" });

        Assert.Null(result.PollOptionId);
        Assert.Null(result.CustomOptionText);
        Assert.Equal(customImageId, result.ImageId);
        Assert.Equal("My outfit", result.Reason);
        Assert.Equal(customImageId, fixture.Votes.Items[0].ImageId);
    }

    [Fact]
    public async Task Cast_ImagePoll_InvalidImageId_Throws()
    {
        var fixture = VoteFixture.CreateImagePoll();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
                fixture.GuestId,
                new CastVoteRequest { ImageId = Guid.NewGuid() }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("imageId", exception.Field);
        Assert.Empty(fixture.Votes.Items);
    }

    [Fact]
    public async Task Cast_ImagePoll_CustomText_Throws()
    {
        var fixture = VoteFixture.CreateImagePoll();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
                fixture.GuestId,
                new CastVoteRequest { CustomOption = "Something else text" }));

        Assert.Equal("customOption", exception.Field);
    }

    [Fact]
    public async Task Cast_TextPoll_CustomImage_Throws()
    {
        var fixture = VoteFixture.Create();
        var imageId = Guid.NewGuid();
        fixture.Images.Existing.Add(imageId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
                fixture.GuestId,
                new CastVoteRequest { ImageId = imageId }));

        Assert.Equal("imageId", exception.Field);
    }

    [Fact]
    public async Task Cast_Duplicate_ThrowsAlreadyVoted()
    {
        var fixture = VoteFixture.Create();
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };
        await fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AlreadyVoted, exception.ErrorCode);
        Assert.Equal("You have already voted in this poll.", exception.Message);
        Assert.Equal("This voter has already voted in this poll.", exception.ErrorMessage);
        Assert.Single(fixture.Votes.Items);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.VoteRejectedAlreadyVoted);
    }

    [Fact]
    public async Task Cast_SameGuestDifferentPolls_BothSucceed()
    {
        var fixture = VoteFixture.Create();
        var other = VoteFixture.CreateTextPoll("Second poll?");
        fixture.Polls.Items.Add(other);

        await fixture.Service.CastAsync(
            fixture.Poll.Id,
            null,
            fixture.GuestId,
            new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id });
        await fixture.Service.CastAsync(
            other.Id,
            null,
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

        await fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request);
        await fixture.Service.CastAsync(fixture.Poll.Id, null, otherGuest, request);

        Assert.Equal(2, fixture.Votes.Items.Count);
    }

    [Fact]
    public async Task Cast_UserAndGuestSamePoll_BothSucceed()
    {
        var fixture = VoteFixture.Create();
        var userId = Guid.NewGuid();
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };

        await fixture.Service.CastAsync(fixture.Poll.Id, userId, null, request);
        await fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request);

        Assert.Equal(2, fixture.Votes.Items.Count);
    }

    [Fact]
    public async Task Cast_ConcurrentDuplicate_UniqueConstraintKeepsOneVote()
    {
        var fixture = VoteFixture.Create();
        fixture.Votes.IgnoreExists = true;
        var request = new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id };

        await fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(fixture.Poll.Id, null, fixture.GuestId, request));

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
                null,
                fixture.GuestId,
                new CastVoteRequest { CustomOption = "Node.js" }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Cast_ExpiredPoll_ThrowsValidationError()
    {
        var fixture = VoteFixture.Create();
        var expired = new PollEntity(
            Guid.NewGuid(),
            "Expired?",
            OptionType.Text,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        expired.AddTextOption("A", 1);
        expired.AddTextOption("B", 2);
        fixture.Polls.Items.Add(expired);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                expired.Id,
                null,
                fixture.GuestId,
                new CastVoteRequest { OptionId = expired.Options.First().Id }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    [Fact]
    public async Task Cast_OptionFromAnotherPoll_ThrowsValidationError()
    {
        var fixture = VoteFixture.Create();
        var other = VoteFixture.CreateTextPoll("Other?");
        fixture.Polls.Items.Add(other);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
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
                null,
                fixture.GuestId,
                new CastVoteRequest
                {
                    OptionId = fixture.Poll.Options.First().Id,
                    CustomOption = "Node.js"
                }));
    }

    [Fact]
    public async Task Cast_OptionAndImage_ThrowsValidation()
    {
        var fixture = VoteFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
                fixture.GuestId,
                new CastVoteRequest
                {
                    OptionId = fixture.Poll.Options.First().Id,
                    ImageId = Guid.NewGuid()
                }));
    }

    [Fact]
    public async Task Cast_NeitherOptionNorCustom_ThrowsValidation()
    {
        var fixture = VoteFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CastAsync(
                fixture.Poll.Id,
                null,
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
                null,
                fixture.GuestId,
                new CastVoteRequest { OptionId = fixture.Poll.Options.First().Id }));

        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    private sealed class VoteFixture
    {
        public required VoteService Service { get; init; }
        public required FakePollRepository Polls { get; init; }
        public required FakeVoteRepository Votes { get; init; }
        public required FakeImageRepository Images { get; init; }
        public required FakeSystemLogWriter Logs { get; init; }
        public required PollEntity Poll { get; init; }
        public Guid GuestId { get; init; } = Guid.NewGuid();

        public static VoteFixture Create()
        {
            return CreateWithPoll(CreateTextPoll("Which option do you prefer?"));
        }

        public static VoteFixture CreateImagePoll()
        {
            var image1 = Guid.NewGuid();
            var image2 = Guid.NewGuid();
            var fixture = CreateWithPoll(CreateImagePollEntity("Which outfit?", image1, image2));
            fixture.Images.Existing.Add(image1);
            fixture.Images.Existing.Add(image2);
            return fixture;
        }

        private static VoteFixture CreateWithPoll(PollEntity poll)
        {
            var polls = new FakePollRepository();
            var votes = new FakeVoteRepository();
            var images = new FakeImageRepository();
            var logs = new FakeSystemLogWriter();
            polls.Items.Add(poll);

            return new VoteFixture
            {
                Service = new VoteService(polls, votes, images, logs, new CastVoteValidator()),
                Polls = polls,
                Votes = votes,
                Images = images,
                Logs = logs,
                Poll = poll
            };
        }

        public static PollEntity CreateTextPoll(string question)
        {
            var poll = new PollEntity(Guid.NewGuid(), question, OptionType.Text);
            poll.AddTextOption("Option A", 1);
            poll.AddTextOption("Option B", 2);
            return poll;
        }

        public static PollEntity CreateImagePollEntity(string question, Guid image1, Guid image2)
        {
            var poll = new PollEntity(Guid.NewGuid(), question, OptionType.Image);
            poll.AddImageOption(image1, 1);
            poll.AddImageOption(image2, 2);
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

        public Task<PollEntity?> GetByIdAndCreatorAsync(
            Guid id,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Items.FirstOrDefault(poll => poll.Id == id && poll.CreatorId == creatorId));
        }

        public Task<IReadOnlyList<PollEntity>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PollEntity> page = Items
                .Where(poll => poll.CreatorId == userId && poll.Status != PollStatus.Deleted)
                .ToList();
            return Task.FromResult(page);
        }

        public Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
            Guid userId,
            DateTime? cursorVotedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<AnsweredPollEntry> page = [];
            return Task.FromResult(page);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeImageRepository : IImageRepository
    {
        public HashSet<Guid> Existing { get; } = [];

        public Task AddAsync(Image image, CancellationToken cancellationToken = default)
        {
            Existing.Add(image.Id);
            return Task.CompletedTask;
        }

        public Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Image?>(null);
        }

        public Task<IReadOnlyCollection<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<Guid> found = ids.Where(Existing.Contains).ToList();
            return Task.FromResult(found);
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

        public Task<bool> ExistsForGuestAsync(
            Guid guestId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            if (IgnoreExists)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(Items.Any(vote => vote.GuestId == guestId && vote.PollId == pollId));
        }

        public Task<bool> ExistsForUserAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            if (IgnoreExists)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(Items.Any(vote => vote.UserId == userId && vote.PollId == pollId));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var guestDuplicates = Items
                .Where(vote => vote.GuestId is not null)
                .GroupBy(vote => (vote.GuestId, vote.PollId))
                .Where(group => group.Count() > 1)
                .ToList();

            var userDuplicates = Items
                .Where(vote => vote.UserId is not null)
                .GroupBy(vote => (vote.UserId, vote.PollId))
                .Where(group => group.Count() > 1)
                .ToList();

            if (guestDuplicates.Count == 0 && userDuplicates.Count == 0)
            {
                return Task.CompletedTask;
            }

            foreach (var extra in guestDuplicates
                         .Concat(userDuplicates)
                         .SelectMany(group => group.Skip(1))
                         .ToList())
            {
                Items.Remove(extra);
            }

            throw VoteExceptions.AlreadyVoted();
        }
    }
}
