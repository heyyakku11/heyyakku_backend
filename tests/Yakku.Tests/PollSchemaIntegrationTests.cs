using DotNet.Testcontainers.Builders;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public bool IsAvailable { get; private set; }

    public string SkipReason { get; private set; } = "Docker is not available, so PostgreSQL integration tests were not run.";

    public string ConnectionString { get; private set; } = string.Empty;

    public YakkuDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<YakkuDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new YakkuDbContext(options);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
            IsAvailable = true;
        }
        catch (Exception exception) when (exception is DockerUnavailableException or TimeoutException)
        {
            IsAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[Collection(PostgresCollection.Name)]
public sealed class PollSchemaIntegrationTests
{
    private static int _userSequence;
    private readonly PostgresFixture _fixture;

    public PollSchemaIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task VoteUniqueConstraint_RejectsASecondVote()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var poll = await AddPollAsync(context, user.Id);
        var optionId = poll.Options.First().Id;
        context.PollVotes.Add(PollVote.ForOption(user.Id, poll.Id, optionId, "Because"));
        await context.SaveChangesAsync();

        context.PollVotes.Add(PollVote.ForCustomAnswer(user.Id, poll.Id, "Other", null));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [SkippableFact]
    public async Task CompositeForeignKey_RejectsAnOptionFromAnotherPoll()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var first = await AddPollAsync(context, user.Id);
        var second = await AddPollAsync(context, user.Id);
        context.PollVotes.Add(PollVote.ForOption(user.Id, first.Id, second.Options.First().Id, null));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [SkippableFact]
    public async Task CheckConstraint_RequiresExactlyOneAnswer()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var poll = await AddPollAsync(context, user.Id);
        var optionId = poll.Options.First().Id;

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO poll_votes (id, poll_id, voter_id, poll_option_id, custom_answer, reason, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {poll.Id}, {user.Id}, {optionId}, {"Both"}, NULL, {DateTime.UtcNow}, {DateTime.UtcNow})
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [SkippableFact]
    public async Task CheckConstraint_RejectsBlankCustomAnswer()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var poll = await AddPollAsync(context, user.Id);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO poll_votes (id, poll_id, voter_id, poll_option_id, custom_answer, reason, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {poll.Id}, {user.Id}, NULL, {"   "}, NULL, {DateTime.UtcNow}, {DateTime.UtcNow})
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [SkippableFact]
    public async Task VoteCounts_AggregateInPostgreSql()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var creator = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var poll = await AddPollAsync(context, creator.Id);
        var optionId = poll.Options.OrderBy(option => option.SortOrder).First().Id;
        context.PollVotes.Add(PollVote.ForOption(creator.Id, poll.Id, optionId, "Because"));
        context.PollVotes.Add(PollVote.ForCustomAnswer(voter.Id, poll.Id, "Rust", null));
        await context.SaveChangesAsync();

        var counts = await new PollRepository(context).GetVoteCountsAsync([poll.Id]);
        var snapshot = counts[poll.Id];

        Assert.Equal(2, snapshot.TotalVotes);
        Assert.Equal(1, snapshot.CustomAnswerCount);
        Assert.Equal(1, snapshot.WhyCount);
        Assert.Equal(1, snapshot.CountFor(optionId));
    }

    [SkippableFact]
    public async Task Cleanup_DoesNotDeleteObjectReferencedByPollMedia()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var poll = await AddPollAsync(context, user.Id, PollContentType.Image);
        var attached = MediaUploadSession.ForUser(
            user.Id,
            poll.Id,
            PollMediaType.Image,
            "image-bucket",
            ".jpg",
            "image/jpeg",
            12,
            1,
            DateTime.UtcNow.AddMinutes(-5));
        var orphan = MediaUploadSession.ForUser(
            user.Id,
            null,
            PollMediaType.Image,
            "image-bucket",
            ".jpg",
            "image/jpeg",
            12,
            2,
            DateTime.UtcNow.AddMinutes(-5));
        poll.AttachMedia(new PollMedia(poll.Id, PollMediaType.Image, attached.BucketName, attached.ObjectKey, "image/jpeg", 12, null, null));
        context.MediaUploadSessions.Add(attached);
        context.MediaUploadSessions.Add(orphan);
        await context.SaveChangesAsync();

        var storage = new FakeObjectStorage();
        var repository = new MediaUploadSessionRepository(context);
        await new MediaUploadCleanupService(repository, storage).CleanupAsync();

        Assert.DoesNotContain(attached.ObjectKey, storage.Deleted);
        Assert.Contains(orphan.ObjectKey, storage.Deleted);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _userSequence);
        var user = new User($"555{sequence:D10}", $"User {sequence}", $"anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Poll> AddPollAsync(
        YakkuDbContext context,
        Guid creatorId,
        PollContentType contentType = PollContentType.Question)
    {
        var poll = new Poll(creatorId, "Which presentation is better?", contentType);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        context.Polls.Add(poll);
        await context.SaveChangesAsync();
        return poll;
    }
}
