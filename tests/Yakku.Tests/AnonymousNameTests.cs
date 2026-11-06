using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Common;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users.AnonymousNames;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Unit tests: no database, no Redis.</summary>
public sealed class AnonymousNameUnitTests
{
    [Fact]
    public void TheGenerator_JoinsOneWordFromEachCategory_AndIsNeverEmpty()
    {
        var generator = new DisplayNameGenerator();
        for (var i = 0; i < 2_000; i++)
        {
            var name = generator.Generate();

            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.True(name.Length <= 50, "must fit the column");
            Assert.True(SplitsIntoThreeWords(name), $"'{name}' is not Adjective+Animal+NatureOrObject");
        }
    }

    [Fact]
    public void TheWordLists_AreCleanAndLargeEnoughForManyUsers()
    {
        foreach (var list in new[] { AnonymousNameWords.Adjectives, AnonymousNameWords.Animals, AnonymousNameWords.NatureAndObjects })
        {
            Assert.Equal(list.Count, list.Select(w => w.ToLowerInvariant()).Distinct().Count());
            Assert.All(list, word =>
            {
                Assert.Matches("^[A-Z][a-z]+$", word);
            });
        }

        Assert.True(AnonymousNameFormat.MaxLength <= 50);
        Assert.True(AnonymousNameWords.Adjectives.Count * AnonymousNameWords.Animals.Count * AnonymousNameWords.NatureAndObjects.Count >= 100_000);
    }

    [Fact]
    public async Task TheAllocator_SkipsNamesTakenInTheDatabaseOrHeldInRedis()
    {
        var users = new StubUsers("TakenOneName", "TakenTwoName");
        var reservations = new MemoryReservations();
        reservations.Hold("HeldInRedisName");
        var allocator = new AnonymousNameAllocator(
            new SequenceGenerator("TakenOneName", "TakenTwoName", "HeldInRedisName", "FreeNameHere"),
            users,
            reservations);

        var name = await allocator.ReserveAsync(TimeSpan.FromMinutes(1));

        Assert.Equal("FreeNameHere", name);
        Assert.True(reservations.IsHeld("FreeNameHere"));
    }

    [Fact]
    public async Task TheAllocator_TreatsNamesCaseInsensitively()
    {
        var allocator = new AnonymousNameAllocator(
            new SequenceGenerator("brave fox", "BRAVEFOX", "OtherName"),
            new StubUsers("bravefox"),
            new MemoryReservations());

        // "BRAVEFOX" collides with the stored "bravefox"; the space variant is a different string and is fine,
        // so the first usable candidate is the first one.
        var name = await allocator.ReserveAsync(TimeSpan.FromMinutes(1));
        Assert.Equal("brave fox", name);
    }

    [Fact]
    public async Task TheAllocator_GivesUp_WithA503_WhenEveryCandidateIsTaken()
    {
        var allocator = new AnonymousNameAllocator(
            new SequenceGenerator("AlwaysTaken"),
            new StubUsers("AlwaysTaken"),
            new MemoryReservations());

        var exception = await Assert.ThrowsAsync<AppException>(() => allocator.ReserveAsync(TimeSpan.FromMinutes(1)));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AnonymousNameUnavailable, exception.ErrorCode);
    }

    [Fact]
    public async Task Regeneration_ReturnsTheNewName_AndReleasesTheHold()
    {
        var reservations = new MemoryReservations();
        var repository = new ScriptedRepository(AnonymousNameUpdateResult.Updated);
        var service = NewService(new SequenceGenerator("NewNameHere"), reservations, repository);

        var response = await service.RegenerateAsync(Guid.NewGuid());

        Assert.Equal("NewNameHere", response.AnonymousName);
        Assert.False(reservations.IsHeld("NewNameHere"));
    }

    [Fact]
    public async Task Regeneration_RetriesWithAFreshName_WhenTheDatabaseReportsACollision()
    {
        var repository = new ScriptedRepository(AnonymousNameUpdateResult.Conflict, AnonymousNameUpdateResult.Conflict, AnonymousNameUpdateResult.Updated);
        var service = NewService(new SequenceGenerator("FirstName", "SecondName", "ThirdName"), new MemoryReservations(), repository);

        var response = await service.RegenerateAsync(Guid.NewGuid());

        Assert.Equal("ThirdName", response.AnonymousName);
        Assert.Equal(["FirstName", "SecondName", "ThirdName"], repository.Attempted);
    }

    [Fact]
    public async Task Regeneration_ExhaustingRetries_Returns503_AndNeverReportsASuccess()
    {
        var repository = new ScriptedRepository(AnonymousNameUpdateResult.Conflict);
        var service = NewService(new RandomGenerator(), new MemoryReservations(), repository);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.RegenerateAsync(Guid.NewGuid()));

        Assert.Equal(503, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AnonymousNameUnavailable, exception.ErrorCode);
        Assert.Equal(OperationalSettings.AnonymousNames.MaxAttempts, repository.Attempted.Count);
    }

    [Fact]
    public async Task Regeneration_ForAUserWithoutAProfile_Returns404()
    {
        var service = NewService(new RandomGenerator(), new MemoryReservations(), new ScriptedRepository(AnonymousNameUpdateResult.ProfileNotFound));

        var exception = await Assert.ThrowsAsync<AppException>(() => service.RegenerateAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public void TheRegenerateEndpoint_IsLimitedToTenCallsPerMinutePerUser()
    {
        Assert.Equal(10, OperationalSettings.AnonymousNames.RegenerateRateLimitPerMinute);
    }

    private static AnonymousNameService NewService(IDisplayNameGenerator generator, MemoryReservations reservations, IAnonymousNameRepository repository)
        => new(new AnonymousNameAllocator(generator, new StubUsers(), reservations), repository);

    private static bool SplitsIntoThreeWords(string name)
    {
        foreach (var adjective in AnonymousNameWords.Adjectives.Where(name.StartsWith))
        {
            foreach (var animal in AnonymousNameWords.Animals.Where(a => name[adjective.Length..].StartsWith(a)))
            {
                if (AnonymousNameWords.NatureAndObjects.Contains(name[(adjective.Length + animal.Length)..]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed class SequenceGenerator(params string[] names) : IDisplayNameGenerator
    {
        private int _next;

        // Repeats the last name forever once the script runs out.
        public string Generate() => names[Math.Min(_next++, names.Length - 1)];
    }

    private sealed class RandomGenerator : IDisplayNameGenerator
    {
        public string Generate() => new DisplayNameGenerator().Generate();
    }

    private sealed class ScriptedRepository(params AnonymousNameUpdateResult[] results) : IAnonymousNameRepository
    {
        public List<string> Attempted { get; } = [];

        public Task<AnonymousNameUpdateResult> TryUpdateAsync(Guid userId, string anonymousName, CancellationToken cancellationToken = default)
        {
            Attempted.Add(anonymousName);
            return Task.FromResult(results[Math.Min(Attempted.Count - 1, results.Length - 1)]);
        }
    }

    internal sealed class MemoryReservations : IAnonymousNameReservations
    {
        private readonly HashSet<string> _held = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _lock = new();

        public void Hold(string name)
        {
            lock (_lock) { _held.Add(name); }
        }

        public bool IsHeld(string name)
        {
            lock (_lock) { return _held.Contains(name); }
        }

        public Task<bool> TryReserveAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
        {
            lock (_lock) { return Task.FromResult(_held.Add(name)); }
        }

        public Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default)
        {
            Hold(name);
            return Task.CompletedTask;
        }

        public Task ReleaseAsync(string name, CancellationToken cancellationToken = default)
        {
            lock (_lock) { _held.Remove(name); }
            return Task.CompletedTask;
        }
    }

    private sealed class StubUsers(params string[] takenNames) : IUserRepository
    {
        private readonly HashSet<string> _taken = new(takenNames, StringComparer.OrdinalIgnoreCase);

        public Task<bool> AnonymousNameExistsAsync(string anonymousName, CancellationToken cancellationToken = default)
            => Task.FromResult(_taken.Contains(anonymousName));

        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void ClearTracking() { }
    }
}

/// <summary>Integration tests against a real Postgres (Docker): the unique index and the atomic update.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AnonymousNameIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public AnonymousNameIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task TheDatabase_RejectsTwoNamesThatDifferOnlyByCase()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var unique = Unique();
        context.Users.Add(NewUser($"BraveFox{unique}"));
        await context.SaveChangesAsync();

        await using var second = _fixture.CreateContext();
        second.Users.Add(NewUser($"bravefox{unique}"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [SkippableFact]
    public async Task TheExistenceCheck_IsCaseInsensitive()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var unique = Unique();
        context.Users.Add(NewUser($"QuietOwl{unique}"));
        await context.SaveChangesAsync();

        var users = new UserRepository(context);

        Assert.True(await users.AnonymousNameExistsAsync($"quietowl{unique}"));
        Assert.True(await users.AnonymousNameExistsAsync($"QUIETOWL{unique}"));
        Assert.False(await users.AnonymousNameExistsAsync($"quietowl{unique}x"));
    }

    [SkippableFact]
    public async Task NewUsersGetUniqueNamesAutomatically()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var reservations = new AnonymousNameUnitTests.MemoryReservations();
        var names = new List<string>();
        for (var i = 0; i < 25; i++)
        {
            await using var context = _fixture.CreateContext();
            var allocator = new AnonymousNameAllocator(new DisplayNameGenerator(), new UserRepository(context), reservations);
            var name = await allocator.ReserveAsync(TimeSpan.FromMinutes(1));
            context.Users.Add(NewUser(name));
            await context.SaveChangesAsync();
            await allocator.ReleaseAsync(name);
            names.Add(name);
        }

        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Select(n => n.ToLowerInvariant()).Distinct().Count());
    }

    [SkippableFact]
    public async Task Regeneration_ChangesOnlyTheCallersName_AndReleasesTheOldOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (mine, theirs) = await AddTwoUsersAsync();
        var oldName = mine.Profile.AnonymousName;
        var theirName = theirs.Profile.AnonymousName;
        var reservations = new AnonymousNameUnitTests.MemoryReservations();

        await using var context = _fixture.CreateContext();
        var response = await NewService(context, reservations).RegenerateAsync(mine.Id);

        await using var check = _fixture.CreateContext();
        var profiles = await check.UserProfiles.AsNoTracking().ToDictionaryAsync(p => p.UserId, p => p.AnonymousName);
        Assert.Equal(response.AnonymousName, profiles[mine.Id]);
        Assert.NotEqual(oldName, profiles[mine.Id]);
        Assert.Equal(theirName, profiles[theirs.Id]);
        Assert.False(await new UserRepository(check).AnonymousNameExistsAsync(oldName), "the previous name is released");
    }

    [SkippableFact]
    public async Task AFailedRegeneration_KeepsTheExistingName()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (mine, theirs) = await AddTwoUsersAsync();
        var original = mine.Profile.AnonymousName;

        await using var context = _fixture.CreateContext();
        // Every candidate is somebody else's name, so every attempt is refused.
        var allocator = new AnonymousNameAllocator(new FixedName(theirs.Profile.AnonymousName.ToUpperInvariant()), new UserRepository(context), new AnonymousNameUnitTests.MemoryReservations());
        var service = new AnonymousNameService(allocator, new AnonymousNameRepository(context));

        var exception = await Assert.ThrowsAsync<AppException>(() => service.RegenerateAsync(mine.Id));

        Assert.Equal(ApiErrorCodes.AnonymousNameUnavailable, exception.ErrorCode);
        await using var check = _fixture.CreateContext();
        Assert.Equal(original, (await check.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == mine.Id)).AnonymousName);
    }

    [SkippableFact]
    public async Task TheRepository_RefusesACollision_ButLeavesTheOldNameInPlace()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (mine, theirs) = await AddTwoUsersAsync();
        var original = mine.Profile.AnonymousName;

        await using var context = _fixture.CreateContext();
        var result = await new AnonymousNameRepository(context).TryUpdateAsync(mine.Id, theirs.Profile.AnonymousName.ToUpperInvariant());

        Assert.Equal(AnonymousNameUpdateResult.Conflict, result);
        await using var check = _fixture.CreateContext();
        Assert.Equal(original, (await check.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == mine.Id)).AnonymousName);
    }

    [SkippableFact]
    public async Task ARegenerationForAMissingProfile_IsReportedAsNotFound()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var result = await new AnonymousNameRepository(context).TryUpdateAsync(Guid.NewGuid(), $"Nobody{Unique()}");

        Assert.Equal(AnonymousNameUpdateResult.ProfileNotFound, result);
    }

    [SkippableFact]
    public async Task ConcurrentRegenerations_ByManyUsers_AllSucceedWithDistinctNames()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var users = new List<User>();
        for (var i = 0; i < 12; i++)
        {
            await using var context = _fixture.CreateContext();
            var user = NewUser($"Seed{Unique()}");
            context.Users.Add(user);
            await context.SaveChangesAsync();
            users.Add(user);
        }

        var reservations = new AnonymousNameUnitTests.MemoryReservations();
        var results = await Task.WhenAll(users.Select(async user =>
        {
            await using var context = _fixture.CreateContext();
            return (await NewService(context, reservations).RegenerateAsync(user.Id)).AnonymousName;
        }));

        Assert.Equal(users.Count, results.Select(n => n.ToLowerInvariant()).Distinct().Count());
    }

    [SkippableFact]
    public async Task ConcurrentRegenerations_ByOneUser_LeaveExactlyOneConsistentName()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (mine, _) = await AddTwoUsersAsync();
        var reservations = new AnonymousNameUnitTests.MemoryReservations();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            return (await NewService(context, reservations).RegenerateAsync(mine.Id)).AnonymousName;
        }));

        await using var check = _fixture.CreateContext();
        var final = (await check.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == mine.Id)).AnonymousName;
        Assert.Contains(final, results);
        Assert.Equal(1, await check.UserProfiles.CountAsync(p => p.UserId == mine.Id));
    }

    private AnonymousNameService NewService(YakkuDbContext context, IAnonymousNameReservations reservations)
        => new(new AnonymousNameAllocator(new DisplayNameGenerator(), new UserRepository(context), reservations), new AnonymousNameRepository(context));

    private async Task<(User Mine, User Theirs)> AddTwoUsersAsync()
    {
        await using var context = _fixture.CreateContext();
        var mine = NewUser($"Mine{Unique()}");
        var theirs = NewUser($"Theirs{Unique()}");
        context.Users.AddRange(mine, theirs);
        await context.SaveChangesAsync();
        return (mine, theirs);
    }

    private static string Unique() => Interlocked.Increment(ref _sequence).ToString("D6") + Guid.NewGuid().ToString("N")[..6];

    private static User NewUser(string anonymousName)
        => new($"+99{Interlocked.Increment(ref _sequence):D12}", anonymousName);

    private sealed class FixedName(string name) : IDisplayNameGenerator
    {
        public string Generate() => name;
    }
}
