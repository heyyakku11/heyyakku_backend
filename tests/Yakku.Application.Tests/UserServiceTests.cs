using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Domain.Entities;
using PollEntity = Yakku.Domain.Entities.Polls;
using Xunit;

namespace Yakku.Application.Tests.Users;

public class UserServiceTests
{
    [Fact]
    public async Task GetMe_WhenUserExists_ReturnsProfile()
    {
        var fixture = UserFixture.Create();
        var user = new User("user@example.com", "yakku@1233");
        fixture.Users.Users.Add(user);

        var result = await fixture.Service.GetMeAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("Active", result.Status);
        Assert.Equal("user@example.com", result.Email);
        Assert.Equal("yakku@1233", result.DisplayName);
        Assert.Equal(user.LastLoginAt, result.LastLoginAt);
        Assert.Null(result.AvatarUrl);
    }

    [Fact]
    public async Task GetMe_WhenUserMissing_ReturnsNull()
    {
        var fixture = UserFixture.Create();

        var result = await fixture.Service.GetMeAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetMyPolls_FirstPage_ReturnsTenAndNextCursor()
    {
        var fixture = UserFixture.Create();
        var userId = Guid.NewGuid();
        for (var i = 0; i < 11; i++)
        {
            fixture.Polls.Items.Add(new PollEntity(userId, $"Question {i}", null));
        }

        var result = await fixture.Service.GetMyPollsAsync(userId, null);

        Assert.Equal(10, result.Items.Count);
        Assert.True(result.Meta.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(result.Meta.NextCursor));
        Assert.Equal(10, result.Meta.PageSize);
        Assert.All(result.Items, item =>
        {
            Assert.NotEqual(Guid.Empty, item.PollId);
            Assert.StartsWith("Question ", item.Question);
            Assert.Equal("Active", item.Status);
            Assert.Null(item.ExpiresAt);
            Assert.NotEqual(default, item.CreatedAt);
            Assert.NotNull(item.PollOptions);
        });
    }

    [Fact]
    public async Task GetMyPolls_SecondPage_ContinuesFromCursor()
    {
        var fixture = UserFixture.Create();
        var userId = Guid.NewGuid();
        for (var i = 0; i < 11; i++)
        {
            fixture.Polls.Items.Add(new PollEntity(userId, $"Question {i}", null));
        }

        var first = await fixture.Service.GetMyPollsAsync(userId, null);
        var second = await fixture.Service.GetMyPollsAsync(userId, first.Meta.NextCursor);

        Assert.Single(second.Items);
        Assert.False(second.Meta.HasMore);
        Assert.Null(second.Meta.NextCursor);
        Assert.DoesNotContain(second.Items[0].PollId, first.Items.Select(item => item.PollId));
    }

    [Fact]
    public async Task GetMyPolls_WhenUserHasNone_ReturnsEmptyPage()
    {
        var fixture = UserFixture.Create();

        var result = await fixture.Service.GetMyPollsAsync(Guid.NewGuid(), null);

        Assert.Empty(result.Items);
        Assert.False(result.Meta.HasMore);
        Assert.Null(result.Meta.NextCursor);
    }

    private sealed class UserFixture
    {
        public FakeUserRepository Users { get; }
        public FakePollRepository Polls { get; }
        public UserService Service { get; }

        private UserFixture(FakeUserRepository users, FakePollRepository polls, UserService service)
        {
            Users = users;
            Polls = polls;
            Service = service;
        }

        public static UserFixture Create()
        {
            var users = new FakeUserRepository();
            var polls = new FakePollRepository();
            var service = new UserService(users, polls);

            return new UserFixture(users, polls, service);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.FirstOrDefault(user => user.Email == email));
        }

        public Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Users.Any(user => user.Profile.DisplayName == displayName));
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
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
