using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Services;
using Yakku.Application.Polls.Validators;
using Yakku.Application.System;
using Yakku.Application.Tests.Fakes;
using PollEntity = Yakku.Domain.Entities.Polls;
using Xunit;

namespace Yakku.Application.Tests.Polls;

public class PollServiceTests
{
    [Fact]
    public async Task Create_SetsCreatorIdFromCaller()
    {
        var polls = new FakePollRepository();
        var logs = new FakeSystemLogWriter();
        var service = new PollService(polls, logs, new CreatePollValidator(), new GetPollValidator());
        var creatorId = Guid.NewGuid();

        var result = await service.CreateAsync(
            new CreatePollRequest
            {
                Question = "Which option do you prefer?",
                Options = ["Option A", "Option B"]
            },
            creatorId);

        Assert.Equal(creatorId, result.CreatorId);
        Assert.Equal(creatorId, polls.Items[0].CreatorId);
        Assert.Equal(result.Id, polls.Items[0].Id);
        Assert.Contains(logs.Entries, entry => entry.EventType == SystemLogEventTypes.PollCreated);
    }

    [Fact]
    public async Task GetById_AppendsSomethingElseSystemOption()
    {
        var polls = new FakePollRepository();
        var service = new PollService(polls, new FakeSystemLogWriter(), new CreatePollValidator(), new GetPollValidator());
        var created = await service.CreateAsync(
            new CreatePollRequest
            {
                Question = "Which option do you prefer?",
                Options = ["Option A", "Option B"]
            },
            Guid.NewGuid());

        var result = await service.GetByIdAsync(created.Id);

        Assert.NotNull(result);
        Assert.Equal(3, result.Options.Count);
        Assert.False(result.Options[0].IsSystemOption);
        Assert.False(result.Options[1].IsSystemOption);
        Assert.Equal("Option A", result.Options[0].Text);
        Assert.Equal("Option B", result.Options[1].Text);

        var system = result.Options[^1];
        Assert.Null(system.Id);
        Assert.Equal("Something else", system.Text);
        Assert.True(system.IsSystemOption);
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
            IEnumerable<PollEntity> query = Items.Where(poll => poll.CreatorId == userId);
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

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
