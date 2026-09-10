using FluentValidation;
using Yakku.Application.Categories.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.Polls;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Services;
using Yakku.Application.Polls.Validators;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Polls;
using Xunit;

namespace Yakku.Application.Tests.Polls;

public class PollServiceTests
{
    [Fact]
    public async Task Create_TextPoll_SetsCreatorAndShareToken()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();

        var result = await fixture.Service.CreateAsync(
            TextRequest("Which option do you prefer?", "Option A", "Option B"),
            creatorId);

        Assert.Equal(creatorId, fixture.Polls.Items[0].CreatorId);
        Assert.Equal(result.Id, fixture.Polls.Items[0].Id);
        Assert.False(string.IsNullOrWhiteSpace(result.ShareToken));
        Assert.Equal("text", result.OptionType);
        Assert.Equal("active", result.Status);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal(1, result.Options[0].SortOrder);
        Assert.Equal(2, result.Options[1].SortOrder);
        Assert.DoesNotContain(result.Options, option => option.Text == "Something else");
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.PollCreated);
    }

    [Fact]
    public async Task Create_ImagePoll_PersistsImageOptions()
    {
        var fixture = PollFixture.Create();
        var image1 = Guid.NewGuid();
        var image2 = Guid.NewGuid();
        fixture.Images.Existing.Add(image1);
        fixture.Images.Existing.Add(image2);

        var result = await fixture.Service.CreateAsync(
            new CreatePollRequest
            {
                Question = "Which outfit?",
                OptionType = "image",
                Options =
                [
                    new CreatePollOptionRequest { ImageId = image1 },
                    new CreatePollOptionRequest { ImageId = image2 }
                ]
            },
            Guid.NewGuid());

        Assert.Equal("image", result.OptionType);
        Assert.Equal(image1, result.Options[0].ImageId);
        Assert.Equal(image2, result.Options[1].ImageId);
        Assert.All(result.Options, option => Assert.Null(option.Text));
    }

    [Fact]
    public async Task Create_MixedImageOnTextPoll_ThrowsValidation()
    {
        var fixture = PollFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CreateAsync(
                new CreatePollRequest
                {
                    Question = "Mixed?",
                    OptionType = "text",
                    Options =
                    [
                        new CreatePollOptionRequest { Text = "Yes", ImageId = Guid.NewGuid() },
                        new CreatePollOptionRequest { Text = "No" }
                    ]
                },
                Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_LessThanTwoOptions_ThrowsValidation()
    {
        var fixture = PollFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CreateAsync(
                new CreatePollRequest
                {
                    Question = "Only one?",
                    OptionType = "text",
                    Options = [new CreatePollOptionRequest { Text = "Only" }]
                },
                Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_InvalidImageId_Throws()
    {
        var fixture = PollFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(
                new CreatePollRequest
                {
                    Question = "Outfit?",
                    OptionType = "image",
                    Options =
                    [
                        new CreatePollOptionRequest { ImageId = Guid.NewGuid() },
                        new CreatePollOptionRequest { ImageId = Guid.NewGuid() }
                    ]
                },
                Guid.NewGuid()));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
    }

    [Fact]
    public async Task Create_InvalidCategory_Throws()
    {
        var fixture = PollFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.CreateAsync(
                new CreatePollRequest
                {
                    Question = "Category?",
                    CategoryId = Guid.NewGuid(),
                    OptionType = "text",
                    Options =
                    [
                        new CreatePollOptionRequest { Text = "A" },
                        new CreatePollOptionRequest { Text = "B" }
                    ]
                },
                Guid.NewGuid()));

        Assert.Equal("categoryId", exception.Field);
    }

    [Fact]
    public async Task GetPollDetails_OwnerOnly_ReturnsOptionsWithoutSomethingElse()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(
            TextRequest("Which option do you prefer?", "Option A", "Option B"),
            creatorId);

        var result = await fixture.Service.GetPollDetailsAsync(created.Id, creatorId);

        Assert.Equal(2, result.Options.Count);
        Assert.Equal("Option A", result.Options[0].Text);
        Assert.Equal("Option B", result.Options[1].Text);
    }

    [Fact]
    public async Task GetPollDetails_OtherUser_ThrowsNotFound()
    {
        var fixture = PollFixture.Create();
        var created = await fixture.Service.CreateAsync(
            TextRequest("Owned poll", "A", "B"),
            Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetPollDetailsAsync(created.Id, Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task ClosePoll_SetsClosedStatus()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Close me", "A", "B"), creatorId);

        var result = await fixture.Service.ClosePollAsync(created.Id, creatorId);

        Assert.Equal("closed", result.Status);
        Assert.NotNull(result.ClosedAt);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.PollClosed);
    }

    [Fact]
    public async Task ClosePoll_AlreadyClosed_ThrowsConflict()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Close twice", "A", "B"), creatorId);
        await fixture.Service.ClosePollAsync(created.Id, creatorId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.ClosePollAsync(created.Id, creatorId));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.Conflict, exception.ErrorCode);
    }

    [Fact]
    public async Task DeletePoll_SoftDeletesAndHidesFromDetails()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Delete me", "A", "B"), creatorId);

        await fixture.Service.DeletePollAsync(created.Id, creatorId);

        Assert.Equal(PollStatus.Deleted, fixture.Polls.Items[0].Status);
        Assert.Contains(fixture.Logs.Entries, entry => entry.EventType == SystemLogEventTypes.PollDeleted);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetPollDetailsAsync(created.Id, creatorId));
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetCreatorPolls_ExcludesDeleted()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var keep = await fixture.Service.CreateAsync(TextRequest("Keep", "A", "B"), creatorId);
        var remove = await fixture.Service.CreateAsync(TextRequest("Remove", "A", "B"), creatorId);
        await fixture.Service.DeletePollAsync(remove.Id, creatorId);

        var page = await fixture.Service.GetCreatorPollsAsync(creatorId, null);

        Assert.Single(page.Items);
        Assert.Equal(keep.Id, page.Items[0].Id);
    }

    private static CreatePollRequest TextRequest(string question, params string[] options)
    {
        return new CreatePollRequest
        {
            Question = question,
            OptionType = "text",
            Options = options
                .Select(text => new CreatePollOptionRequest { Text = text })
                .ToList()
        };
    }

    private sealed class PollFixture
    {
        public required PollService Service { get; init; }
        public required FakePollRepository Polls { get; init; }
        public required FakeImageRepository Images { get; init; }
        public required FakeSystemLogWriter Logs { get; init; }

        public static PollFixture Create()
        {
            var polls = new FakePollRepository();
            var images = new FakeImageRepository();
            var categories = new FakeCategoryRepository();
            var logs = new FakeSystemLogWriter();

            return new PollFixture
            {
                Service = new PollService(
                    polls,
                    categories,
                    images,
                    logs,
                    new CreatePollValidator()),
                Polls = polls,
                Images = images,
                Logs = logs
            };
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
            IEnumerable<PollEntity> query = Items.Where(poll =>
                poll.CreatorId == userId && poll.Status != PollStatus.Deleted);
            if (cursorCreatedAt is not null && cursorId is not null)
            {
                query = query.Where(poll =>
                    poll.CreatedAt < cursorCreatedAt.Value
                    || (poll.CreatedAt == cursorCreatedAt.Value && poll.Id < cursorId.Value));
            }

            IReadOnlyList<PollEntity> page = query
                .OrderByDescending(poll => poll.CreatedAt)
                .ThenByDescending(poll => poll.Id)
                .Take(take)
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

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        public Task<Category?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Category?>(null);
        }
    }
}
