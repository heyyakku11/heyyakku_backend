using System.Reflection;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Tests.Polls;

public class SavedPollServiceTests
{
    [Fact]
    public async Task Save_NewPoll_StoresJoin()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var poll = new PollEntity(userId, "Save me", OptionType.Text);
        fixture.Polls.Items.Add(poll);

        var result = await fixture.Service.SaveAsync(userId, poll.Id);

        Assert.True(result.Saved);
        Assert.Equal(poll.Id, result.PollId);
        Assert.Single(fixture.Saves.Items);
        Assert.Equal(userId, fixture.Saves.Items[0].UserId);
        Assert.Equal(poll.Id, fixture.Saves.Items[0].PollId);
    }

    [Fact]
    public async Task Save_AlreadySaved_StaysSavedWithoutDuplicate()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var poll = new PollEntity(userId, "Save twice", OptionType.Text);
        fixture.Polls.Items.Add(poll);

        await fixture.Service.SaveAsync(userId, poll.Id);
        var result = await fixture.Service.SaveAsync(userId, poll.Id);

        Assert.True(result.Saved);
        Assert.Single(fixture.Saves.Items);
    }

    [Fact]
    public async Task Save_UniqueRace_StillReturnsSaved()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var poll = new PollEntity(userId, "Race", OptionType.Text);
        fixture.Polls.Items.Add(poll);
        fixture.Saves.ConflictOnSave = true;

        var result = await fixture.Service.SaveAsync(userId, poll.Id);

        Assert.True(result.Saved);
        Assert.Equal(poll.Id, result.PollId);
        Assert.Empty(fixture.Saves.Items);
    }

    [Fact]
    public async Task Unsave_RemovesJoin()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var poll = new PollEntity(userId, "Unsave me", OptionType.Text);
        fixture.Polls.Items.Add(poll);
        await fixture.Service.SaveAsync(userId, poll.Id);

        var result = await fixture.Service.UnsaveAsync(userId, poll.Id);

        Assert.False(result.Saved);
        Assert.Equal(poll.Id, result.PollId);
        Assert.Empty(fixture.Saves.Items);
    }

    [Fact]
    public async Task Unsave_WhenNothingSaved_ReturnsNotSaved()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Service.UnsaveAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.Saved);
        Assert.Empty(fixture.Saves.Items);
    }

    [Fact]
    public async Task Save_MissingOrDeletedPoll_ReturnsNotFound()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var deleted = new PollEntity(userId, "Gone", OptionType.Text);
        Assert.True(deleted.TrySoftDelete());
        fixture.Polls.Items.Add(deleted);

        var missing = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SaveAsync(userId, Guid.NewGuid()));
        var removed = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.SaveAsync(userId, deleted.Id));

        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, missing.ErrorCode);
        Assert.Equal(404, removed.StatusCode);
        Assert.Empty(fixture.Saves.Items);
    }

    [Fact]
    public async Task List_ReturnsOnlyThatUsersNonDeletedPollsNewestFirst()
    {
        var fixture = Fixture.Create();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var older = new PollEntity(userId, "Older", OptionType.Text);
        var newer = new PollEntity(userId, "Newer", OptionType.Text);
        var deleted = new PollEntity(userId, "Deleted", OptionType.Text);
        var other = new PollEntity(otherUserId, "Other", OptionType.Text);
        fixture.Polls.Items.AddRange([older, newer, deleted, other]);

        await fixture.Service.SaveAsync(userId, older.Id);
        await fixture.Service.SaveAsync(userId, newer.Id);
        await fixture.Service.SaveAsync(userId, deleted.Id);
        await fixture.Service.SaveAsync(otherUserId, other.Id);
        Assert.True(deleted.TrySoftDelete());
        SetSavedAt(fixture.Saves.Items.Single(saved => saved.PollId == older.Id), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        SetSavedAt(fixture.Saves.Items.Single(saved => saved.PollId == newer.Id), new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await fixture.Service.ListAsync(userId, null);

        Assert.Equal([newer.Id, older.Id], result.Items.Select(poll => poll.Id));
        Assert.Equal("Newer", result.Items[0].Question);
        Assert.Equal(10, result.Meta.PageSize);
        Assert.False(result.Meta.HasMore);
    }

    private static void SetSavedAt(SavedPoll savedPoll, DateTime savedAt)
    {
        typeof(SavedPoll).GetProperty(nameof(SavedPoll.SavedAt))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(savedPoll, [savedAt]);
    }

    private sealed class Fixture
    {
        public FakePollRepository Polls { get; }
        public FakeSavedPollRepository Saves { get; }
        public SavedPollService Service { get; }

        private Fixture(FakePollRepository polls, FakeSavedPollRepository saves, SavedPollService service)
        {
            Polls = polls;
            Saves = saves;
            Service = service;
        }

        public static Fixture Create()
        {
            var polls = new FakePollRepository();
            var saves = new FakeSavedPollRepository(polls);
            var service = new SavedPollService(saves, polls);
            return new Fixture(polls, saves, service);
        }
    }

    private sealed class FakeSavedPollRepository : ISavedPollRepository
    {
        private readonly FakePollRepository _polls;
        private SavedPoll? _pending;

        public FakeSavedPollRepository(FakePollRepository polls)
        {
            _polls = polls;
        }

        public List<SavedPoll> Items { get; } = [];
        public bool ConflictOnSave { get; set; }

        public Task<SavedPoll?> GetByUserAndPollAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(saved => saved.UserId == userId && saved.PollId == pollId));
        }

        public Task AddAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default)
        {
            _pending = savedPoll;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default)
        {
            Items.Remove(savedPoll);
            return Task.CompletedTask;
        }

        public Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ConflictOnSave && _pending is not null)
            {
                _pending = null;
                return Task.FromResult(false);
            }

            if (_pending is not null && Items.All(saved => saved.Id != _pending.Id))
            {
                Items.Add(_pending);
            }

            _pending = null;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<SavedPoll>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<SavedPoll> query = Items.Where(saved =>
                saved.UserId == userId
                && _polls.Items.Any(poll => poll.Id == saved.PollId && poll.Status != PollStatus.Deleted));

            if (cursorSavedAt is not null && cursorPollId is not null)
            {
                query = query.Where(saved =>
                    saved.SavedAt < cursorSavedAt.Value
                    || (saved.SavedAt == cursorSavedAt.Value && saved.PollId < cursorPollId.Value));
            }

            IReadOnlyList<SavedPoll> page = query
                .OrderByDescending(saved => saved.SavedAt)
                .ThenByDescending(saved => saved.PollId)
                .Take(take)
                .ToList();

            foreach (var saved in page)
            {
                var poll = _polls.Items.Single(item => item.Id == saved.PollId);
                typeof(SavedPoll).GetProperty(nameof(SavedPoll.Poll))!
                    .GetSetMethod(nonPublic: true)!
                    .Invoke(saved, [poll]);
            }

            return Task.FromResult(page);
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

        public Task<PollEntity?> GetByShareTokenAsync(string shareToken, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PollEntity?>(null);
        }

        public Task<PollEntity?> GetByIdAndCreatorAsync(
            Guid id,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PollEntity?>(null);
        }

        public Task<IReadOnlyList<PollEntity>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PollEntity>>([]);
        }

        public Task<IReadOnlyList<PollEntity>> GetVisiblePollsAsync(
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PollEntity>>([]);
        }

        public Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
            Guid userId,
            DateTime? cursorVotedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AnsweredPollEntry>>([]);
        }

        public Task<IReadOnlyList<PollEntity>> ListByStatusAsync(
            PollStatus? status,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PollEntity>>([]);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
