using Yakku.Application.Caching;
using Yakku.Application.Checks;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Themes;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Polls;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Services;
using Yakku.Application.Sharing.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Write paths drop the right response-cache keys so the next GET is a miss.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ResponseCacheInvalidationTests
{
    private readonly PostgresFixture _fixture;
    private static int _userSequence;

    public ResponseCacheInvalidationTests(PostgresFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task MakeWeeklyCheck_RemovesUserWeekAndFunListKeys()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var cache = new RecordingResponseCache();
        var repository = new CheckRepository(context);
        var service = new CheckService(repository, new CastCheckVoteValidator(), new NoOpStreakService(), cache: cache);

        var themes = new CheckThemeService(new CheckThemeRepository(context), new CreateCheckThemeValidator());
        var admin = new AdminCheckService(repository, new SaveCheckValidator(), themes);
        var theme = await themes.CreateAsync(ThemeSamples.Request($"Cache-{Guid.NewGuid():N}"[..16]));
        await admin.CreateAsync(new SaveCheckRequest
        {
            CheckTypeId = theme.Id,
            WeekStart = CheckWeek.Current(),
            Question = "Cache make?",
            Options = ["A", "B"],
            IsActive = true
        });

        var user = await AddUserAsync(context);
        var check = Assert.Single(await service.GetChecksAsync(user.Id));
        cache.Removed.Clear();

        await service.MakeAsync(user.Id, new MakeCheckRequest { CheckId = check.Id, CustomAnswer = "mine" });

        var week = CheckWeek.Current();
        Assert.Contains(ResponseCacheKeys.ChecksWeekUser(week, user.Id), cache.Removed);
        Assert.Contains(ResponseCacheKeys.FunList(user.Id), cache.Removed);
        Assert.Contains(ResponseCacheKeys.FunTypes(user.Id), cache.Removed);
    }

    [SkippableFact]
    public async Task Vote_RemovesShareResultAndViewerKeys()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var cache = new RecordingResponseCache();

        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var poll = new Poll(owner.Id, "Share cache?", PollContentType.Question);
        poll.AddTextOption("Yes", 1);
        poll.AddTextOption("No", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();

        var optionId = poll.Options.OrderBy(o => o.SortOrder).First().Id;
        var voting = new VotingService(
            new VotableContentRepository(context),
            new FakeObjectStorage(),
            MediaUploadPolicy.Default,
            new VoteRequestValidator(),
            new RecordingAuditLogWriter(),
            new NoOpStreakService(),
            new RecordingVoteNotificationOutbox(),
            cache: cache);

        await voting.VoteAsync(
            new VoteRequest
            {
                ShareCode = poll.ShareCode,
                OptionId = optionId
            },
            new VoterIdentity(voter.Id, VoterRole.User));

        Assert.Contains(ResponseCacheKeys.ShareResult(poll.ShareCode), cache.Removed);
        Assert.Contains(ResponseCacheKeys.ShareViewer(poll.ShareCode, "anon"), cache.Removed);
        Assert.Contains(ResponseCacheKeys.ShareViewer(poll.ShareCode, owner.Id.ToString("N")), cache.Removed);
        Assert.Contains(ResponseCacheKeys.ShareViewer(poll.ShareCode, voter.Id.ToString("N")), cache.Removed);
        Assert.Contains(ResponseCacheKeys.PollOptionReasons(poll.Id, optionId, owner.Id), cache.Removed);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _userSequence);
        var user = new User($"557{sequence:D10}", $"cache-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>Records removals; GetOrSet always runs the factory (no store).</summary>
    private sealed class RecordingResponseCache : IResponseCache
    {
        public HashSet<string> Removed { get; } = new(StringComparer.Ordinal);

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Removed.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            foreach (var key in keys)
            {
                Removed.Add(key);
            }

            return Task.CompletedTask;
        }
    }
}
