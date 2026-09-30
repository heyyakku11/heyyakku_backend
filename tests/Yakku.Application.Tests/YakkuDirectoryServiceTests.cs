using System.Reflection;
using System.Text.Json;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.YakkuDirectory.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Tests.YakkuDirectory;

public class YakkuDirectoryServiceTests
{
    [Fact]
    public async Task ListUsers_AllAndDefault_ReturnsEveryProfileNewestFirst()
    {
        var fixture = Fixture.Create();
        var older = UserAt("older@example.com", "older", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = UserAt("newer@example.com", "newer", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        SetStatus(older, UserStatus.Disabled);
        fixture.Users.Items.Add(older);
        fixture.Users.Items.Add(newer);

        var omitted = await fixture.Service.ListUsersAsync(null);
        var all = await fixture.Service.ListUsersAsync("all");

        Assert.Equal(["newer@example.com", "older@example.com"], omitted.Select(user => user.Email));
        Assert.Equal(omitted.Select(user => user.Id), all.Select(user => user.Id));
        Assert.Equal("active", omitted[0].Status);
        Assert.Equal("newer", omitted[0].DisplayName);
        Assert.Equal("nonactive", omitted[1].Status);
        Assert.Equal(newer.CreatedAt, omitted[0].CreatedAt);
    }

    [Fact]
    public async Task ListUsers_ActiveAndNonActive_FilterByStatus()
    {
        var fixture = Fixture.Create();
        var active = new User("active@example.com", "active");
        var disabled = new User("disabled@example.com", "disabled");
        SetStatus(disabled, UserStatus.Disabled);
        fixture.Users.Items.Add(active);
        fixture.Users.Items.Add(disabled);

        var activeResult = await fixture.Service.ListUsersAsync("ACTIVE");
        var nonActiveResult = await fixture.Service.ListUsersAsync("nonactive");

        Assert.Equal([active.Id], activeResult.Select(user => user.Id));
        Assert.Equal("active", activeResult[0].Status);
        Assert.Equal([disabled.Id], nonActiveResult.Select(user => user.Id));
        Assert.Equal("nonactive", nonActiveResult[0].Status);
    }

    [Fact]
    public async Task ListGuests_ActiveAndNonActive_FilterByExpiry()
    {
        var fixture = Fixture.Create();
        var active = new Guest("active-token-hash");
        var expired = new Guest("expired-token-hash");
        SetExpiresAt(expired, DateTime.UtcNow.AddMinutes(-1));
        fixture.Guests.Items.Add(active);
        fixture.Guests.Items.Add(expired);

        var all = await fixture.Service.ListGuestsAsync("all");
        var activeResult = await fixture.Service.ListGuestsAsync("active");
        var nonActiveResult = await fixture.Service.ListGuestsAsync("nonactive");

        Assert.Equal(2, all.Count);
        Assert.Equal([active.Id], activeResult.Select(guest => guest.Id));
        Assert.Equal("active", activeResult[0].Status);
        Assert.Equal(active.ExpiresAt, activeResult[0].ExpiresAt);
        Assert.Equal([expired.Id], nonActiveResult.Select(guest => guest.Id));
        Assert.Equal("nonactive", nonActiveResult[0].Status);
        Assert.DoesNotContain("active-token-hash", JsonSerializer.Serialize(all));
        Assert.DoesNotContain("expired-token-hash", JsonSerializer.Serialize(all));
    }

    [Fact]
    public async Task ListPolls_FiltersByStatus()
    {
        var fixture = Fixture.Create();
        var active = new PollEntity(Guid.NewGuid(), "Active question", OptionType.Text);
        var draft = new PollEntity(Guid.NewGuid(), "Draft question", OptionType.Image);
        var closed = new PollEntity(Guid.NewGuid(), "Closed question", OptionType.Text);
        var deleted = new PollEntity(Guid.NewGuid(), "Deleted question", OptionType.Text);
        SetStatus(draft, PollStatus.Draft);
        Assert.True(closed.TryClose());
        Assert.True(deleted.TrySoftDelete());
        fixture.Polls.Items.AddRange([active, draft, closed, deleted]);

        var all = await fixture.Service.ListPollsAsync(null);
        var activeResult = await fixture.Service.ListPollsAsync("active");
        var draftResult = await fixture.Service.ListPollsAsync("draft");
        var closedResult = await fixture.Service.ListPollsAsync("closed");
        var deletedResult = await fixture.Service.ListPollsAsync("deleted");

        Assert.Equal(4, all.Count);
        Assert.Equal([active.Id], activeResult.Select(poll => poll.Id));
        Assert.Equal("active", activeResult[0].Status);
        Assert.Equal("text", activeResult[0].OptionType);
        Assert.Equal(active.Question, activeResult[0].Question);
        Assert.Equal(active.CreatorId, activeResult[0].CreatorId);
        Assert.Equal([draft.Id], draftResult.Select(poll => poll.Id));
        Assert.Equal("draft", draftResult[0].Status);
        Assert.Equal("image", draftResult[0].OptionType);
        Assert.Equal([closed.Id], closedResult.Select(poll => poll.Id));
        Assert.Equal([deleted.Id], deletedResult.Select(poll => poll.Id));
    }

    [Theory]
    [InlineData("users", "banned")]
    [InlineData("guests", "offline")]
    [InlineData("polls", "paused")]
    public async Task InvalidStatus_ReturnsValidationError(string resource, string status)
    {
        var fixture = Fixture.Create();

        var exception = resource switch
        {
            "users" => await Assert.ThrowsAsync<AppException>(() => fixture.Service.ListUsersAsync(status)),
            "guests" => await Assert.ThrowsAsync<AppException>(() => fixture.Service.ListGuestsAsync(status)),
            _ => await Assert.ThrowsAsync<AppException>(() => fixture.Service.ListPollsAsync(status))
        };

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.ValidationError, exception.ErrorCode);
        Assert.Equal("status", exception.Field);
    }

    private static User UserAt(string email, string displayName, DateTime createdAt)
    {
        var user = new User(email, displayName);
        SetCreatedAt(user, createdAt);
        return user;
    }

    private static void SetStatus(User user, UserStatus status)
    {
        SetProperty(user, nameof(User.Status), status);
    }

    private static void SetStatus(PollEntity poll, PollStatus status)
    {
        SetProperty(poll, nameof(PollEntity.Status), status);
    }

    private static void SetExpiresAt(Guest guest, DateTime expiresAt)
    {
        SetProperty(guest, nameof(Guest.ExpiresAt), expiresAt);
    }

    private static void SetCreatedAt(User user, DateTime createdAt)
    {
        SetProperty(user, nameof(User.CreatedAt), createdAt);
    }

    private static void SetProperty(object target, string propertyName, object value)
    {
        target.GetType().GetProperty(propertyName)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(target, [value]);
    }

    private sealed class Fixture
    {
        public FakeUserRepository Users { get; }
        public FakeGuestRepository Guests { get; }
        public FakePollRepository Polls { get; }
        public YakkuDirectoryService Service { get; }

        private Fixture(
            FakeUserRepository users,
            FakeGuestRepository guests,
            FakePollRepository polls,
            YakkuDirectoryService service)
        {
            Users = users;
            Guests = guests;
            Polls = polls;
            Service = service;
        }

        public static Fixture Create()
        {
            var users = new FakeUserRepository();
            var guests = new FakeGuestRepository();
            var polls = new FakePollRepository();
            var service = new YakkuDirectoryService(users, guests, polls);
            return new Fixture(users, guests, polls, service);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(user => user.Id == id));
        }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(user => user.Email == email));
        }

        public Task<IReadOnlyList<User>> ListAsync(UserStatus? status, CancellationToken cancellationToken = default)
        {
            IEnumerable<User> query = Items;
            if (status is not null)
            {
                query = query.Where(user => user.Status == status);
            }

            IReadOnlyList<User> result = query
                .OrderByDescending(user => user.CreatedAt)
                .ThenByDescending(user => user.Id)
                .ToList();
            return Task.FromResult(result);
        }

        public Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGuestRepository : IGuestRepository
    {
        public List<Guest> Items { get; } = [];

        public Task AddAsync(Guest guest, CancellationToken cancellationToken = default)
        {
            Items.Add(guest);
            return Task.CompletedTask;
        }

        public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(guest => guest.GuestTokenHash == tokenHash));
        }

        public Task<IReadOnlyList<Guest>> ListAsync(
            bool? active,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Guest> query = Items;
            if (active == true)
            {
                query = query.Where(guest => guest.ExpiresAt > utcNow);
            }
            else if (active == false)
            {
                query = query.Where(guest => guest.ExpiresAt <= utcNow);
            }

            IReadOnlyList<Guest> result = query
                .OrderByDescending(guest => guest.CreatedAt)
                .ThenByDescending(guest => guest.Id)
                .ToList();
            return Task.FromResult(result);
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
            IEnumerable<PollEntity> query = Items;
            if (status is not null)
            {
                query = query.Where(poll => poll.Status == status);
            }

            IReadOnlyList<PollEntity> result = query
                .OrderByDescending(poll => poll.CreatedAt)
                .ThenByDescending(poll => poll.Id)
                .ToList();
            return Task.FromResult(result);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
