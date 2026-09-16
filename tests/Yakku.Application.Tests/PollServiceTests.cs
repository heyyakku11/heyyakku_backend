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
using PollEntity = Yakku.Domain.Entities.Poll;
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
        Assert.Equal(PollStatus.Active, fixture.Polls.Items[0].Status);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal(1, result.Options[0].SortOrder);
        Assert.Equal(2, result.Options[1].SortOrder);
        Assert.Equal(result.Options[0].Id, result.SelectedOptionId);
        Assert.Equal(1, result.TotalVoteCount);
        Assert.Equal(1, result.Options[0].VoteCount);
        Assert.Equal(100m, result.Options[0].Percentage);
        Assert.Equal(0, result.Options[1].VoteCount);
        Assert.Equal(0, result.Options[1].Percentage);
        Assert.Single(fixture.Polls.Items[0].Votes);
        Assert.Equal(creatorId, fixture.Polls.Items[0].Votes.First().UserId);
        Assert.Equal(result.Options[0].Id, fixture.Polls.Items[0].Votes.First().PollOptionId);
        Assert.Null(fixture.Polls.Items[0].Votes.First().Reason);
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
                SelectedOptionIndex = 1,
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
        Assert.Equal(result.Options[1].Id, result.SelectedOptionId);
        Assert.Equal(1, result.TotalVoteCount);
        Assert.Equal(1, result.Options[1].VoteCount);
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
                    SelectedOptionIndex = 0,
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
                    SelectedOptionIndex = 0,
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
                    SelectedOptionIndex = 0,
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
                    SelectedOptionIndex = 0,
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
    public async Task Create_SelectedOptionIndexOutOfRange_ThrowsValidation()
    {
        var fixture = PollFixture.Create();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.CreateAsync(
                new CreatePollRequest
                {
                    Question = "Out of range?",
                    OptionType = "text",
                    SelectedOptionIndex = 2,
                    Options =
                    [
                        new CreatePollOptionRequest { Text = "A" },
                        new CreatePollOptionRequest { Text = "B" }
                    ]
                },
                Guid.NewGuid()));
    }

    [Fact]
    public async Task GetPollDetails_ReturnsOptionsWithoutSomethingElse()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(
            TextRequest("Which option do you prefer?", "Option A", "Option B"),
            creatorId);

        var result = await fixture.Service.GetPollDetailsAsync(created.Id);

        Assert.Equal(2, result.Options.Count);
        Assert.Equal("Option A", result.Options[0].Text);
        Assert.Equal("Option B", result.Options[1].Text);
    }

    [Fact]
    public async Task GetPollDetails_UnknownId_ThrowsNotFound()
    {
        var fixture = PollFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetPollDetailsAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetSharedPollByToken_ActivePoll_ReturnsDetailsAndAcceptsVotes()
    {
        var fixture = PollFixture.Create();
        var category = new Category("Sports", "sports");
        var created = await fixture.Service.CreateAsync(
            TextRequest("Share me?", "Yes", "No"),
            Guid.NewGuid());
        var poll = fixture.Polls.Items[0];
        typeof(PollEntity)
            .GetProperty(nameof(PollEntity.Category))!
            .SetValue(poll, category);

        var result = await fixture.Service.GetSharedPollByTokenAsync(created.ShareToken);

        Assert.Equal("Share me?", result.Question);
        Assert.Equal("text", result.OptionType);
        Assert.Equal("active", result.Status);
        Assert.True(result.IsAcceptingVotes);
        Assert.Null(result.ExpiresAt);
        Assert.Equal(2, result.Options.Count);
        Assert.Equal("Yes", result.Options[0].Text);
        Assert.Equal("No", result.Options[1].Text);
        Assert.NotNull(result.Category);
        Assert.Equal(category.Id, result.Category!.Id);
        Assert.Equal("Sports", result.Category.Name);
    }

    [Fact]
    public async Task GetSharedPollByToken_ExpiredPoll_ThrowsNotAvailable()
    {
        var fixture = PollFixture.Create();
        var expired = new PollEntity(
            Guid.NewGuid(),
            "Already expired?",
            OptionType.Text,
            expiresAt: DateTime.UtcNow.AddMinutes(-1));
        expired.AddTextOption("A", 1);
        expired.AddTextOption("B", 2);
        fixture.Polls.Items.Add(expired);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetSharedPollByTokenAsync(expired.ShareToken));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.Equal("Poll is not available.", exception.Message);
    }

    [Fact]
    public async Task GetSharedPollByToken_ClosedPoll_ThrowsNotAvailable()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Close share", "A", "B"), creatorId);
        await fixture.Service.ClosePollAsync(created.Id, creatorId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetSharedPollByTokenAsync(created.ShareToken));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.Equal("Poll is not available.", exception.Message);
    }

    [Fact]
    public async Task GetSharedPollByToken_UnknownToken_ThrowsNotAvailable()
    {
        var fixture = PollFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetSharedPollByTokenAsync("missing-token"));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.Equal("Poll is not available.", exception.Message);
    }

    [Fact]
    public async Task GetSharedPollByToken_DeletedPoll_ThrowsNotAvailable()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Delete share", "A", "B"), creatorId);
        await fixture.Service.DeletePollAsync(created.Id, creatorId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.GetSharedPollByTokenAsync(created.ShareToken));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.Equal("Poll is not available.", exception.Message);
    }

    [Fact]
    public async Task ClosePoll_SetsClosedStatus()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var created = await fixture.Service.CreateAsync(TextRequest("Close me", "A", "B"), creatorId);

        var result = await fixture.Service.ClosePollAsync(created.Id, creatorId);

        Assert.Equal(PollStatus.Closed, fixture.Polls.Items[0].Status);
        Assert.NotNull(fixture.Polls.Items[0].ClosedAt);
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
            fixture.Service.GetPollDetailsAsync(created.Id));
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetPolls_ExcludesDeletedAndIncludesShareToken()
    {
        var fixture = PollFixture.Create();
        var creatorId = Guid.NewGuid();
        var keep = await fixture.Service.CreateAsync(TextRequest("Keep", "A", "B"), creatorId);
        var remove = await fixture.Service.CreateAsync(TextRequest("Remove", "A", "B"), creatorId);
        await fixture.Service.DeletePollAsync(remove.Id, creatorId);

        var page = await fixture.Service.GetPollsAsync(null);

        Assert.Single(page.Items);
        Assert.Equal(keep.Id, page.Items[0].Id);
        Assert.False(string.IsNullOrWhiteSpace(page.Items[0].ShareToken));
        Assert.Equal(keep.ShareToken, page.Items[0].ShareToken);
        Assert.Equal(2, page.Items[0].Options.Count);
        Assert.Equal(keep.Options.Select(o => o.Id), page.Items[0].Options.Select(o => o.Id));
    }

    private static CreatePollRequest TextRequest(string question, params string[] options)
    {
        return new CreatePollRequest
        {
            Question = question,
            OptionType = "text",
            SelectedOptionIndex = 0,
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

        public Task<PollEntity?> GetByShareTokenAsync(
            string shareToken,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Items.FirstOrDefault(poll => poll.ShareToken == shareToken));
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

        public Task<IReadOnlyList<PollEntity>> GetVisiblePollsAsync(
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<PollEntity> query = Items.Where(poll => poll.Status != PollStatus.Deleted);
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
