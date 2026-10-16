using Microsoft.EntityFrameworkCore;
using Yakku.Application.Guests;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

[Collection(PostgresCollection.Name)]
public sealed class GuestAtomicityIntegrationTests
{
    private static readonly GuestTokenHasher Hasher = new();
    private readonly PostgresFixture _fixture;

    public GuestAtomicityIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task Claim_MovesSessionsAndDeletesTheGuest_InOneStep()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestAsync(context, sessions: 3);

        var result = await NewClaimService(context).ClaimAndDeleteGuestAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Claimed, result);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
        var sessions = await verify.MediaUploadSessions.Where(s => s.UserId == user.Id).ToListAsync();
        Assert.Equal(3, sessions.Count);
        Assert.All(sessions, s => Assert.Null(s.GuestId));
    }

    [SkippableFact]
    public async Task Claim_RollsBackSessionTransfer_WhenTheGuestDeleteFails()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestAsync(context, sessions: 2);
        var audit = new RecordingAuditLogWriter();
        var failing = new InstrumentedGuestRepository(new GuestRepository(context));
        var service = new GuestMediaClaimService(failing, Hasher, new MediaUploadSessionRepository(context), new DeviceRepository(context), audit);

        // The session reassignment has already run inside the transaction when the guest delete fails.
        var result = await service.TryClaimAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Failed, result);
        Assert.True(failing.SaveWasAttempted);
        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
        var sessions = await verify.MediaUploadSessions.Where(s => s.GuestId == guest.Id).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s => Assert.Null(s.UserId));
        Assert.Single(audit.Authentication, entry => entry.EventType == "guest.claim_failed");
    }

    [SkippableFact]
    public async Task Claim_CanBeRetriedAfterARolledBackAttempt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var failingContext = _fixture.CreateContext();
        var user = await AddUserAsync(failingContext);
        var (guest, raw) = await AddGuestAsync(failingContext, sessions: 1);
        var failing = new InstrumentedGuestRepository(new GuestRepository(failingContext));
        var first = await new GuestMediaClaimService(
                failing,
                Hasher,
                new MediaUploadSessionRepository(failingContext),
                new DeviceRepository(failingContext),
                new RecordingAuditLogWriter())
            .TryClaimAsync(raw, user.Id);
        Assert.Equal(GuestClaimResult.Failed, first);

        await using var retryContext = _fixture.CreateContext();
        var second = await NewClaimService(retryContext).TryClaimAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Claimed, second);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
    }

    [SkippableFact]
    public async Task Claim_RejectsAnExpiredGuest_AndChangesNothing()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestAsync(context, sessions: 1, createdDaysAgo: 20);

        var result = await NewClaimService(context).ClaimAndDeleteGuestAsync(raw, user.Id);

        Assert.Equal(GuestClaimResult.Expired, result);
        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
        Assert.All(
            await verify.MediaUploadSessions.Where(s => s.GuestId == guest.Id).ToListAsync(),
            s => Assert.Null(s.UserId));
    }

    [SkippableFact]
    public async Task ConcurrentClaims_OfTheSameGuest_HaveExactlyOneWinner()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var users = new List<User>();
        for (var i = 0; i < 5; i++)
        {
            users.Add(await AddUserAsync(setup));
        }

        var (guest, raw) = await AddGuestAsync(setup, sessions: 4);

        var results = await Task.WhenAll(users.Select(async user =>
        {
            await using var context = _fixture.CreateContext();
            var service = new GuestMediaClaimService(
                new InstrumentedGuestRepository(
                    new GuestRepository(context),
                    failSave: false,
                    holdAfterLoad: TimeSpan.FromMilliseconds(400)),
                Hasher,
                new MediaUploadSessionRepository(context),
                new DeviceRepository(context),
                new RecordingAuditLogWriter());
            return (user.Id, Result: await service.TryClaimAsync(raw, user.Id));
        }));

        // A lost race is a clean "already claimed", never an error.
        Assert.Equal(1, results.Count(r => r.Result == GuestClaimResult.Claimed));
        Assert.Equal(4, results.Count(r => r.Result == GuestClaimResult.NotFound));
        var winner = results.Single(r => r.Result == GuestClaimResult.Claimed).Id;

        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
        var owned = await verify.MediaUploadSessions
            .Where(s => s.UserId != null && users.Select(u => u.Id).Contains(s.UserId.Value))
            .ToListAsync();
        Assert.Equal(4, owned.Count);
        Assert.All(owned, s => Assert.Equal(winner, s.UserId));
    }

    [SkippableFact]
    public async Task Cleanup_RemovesExpiredGuestsAndEverythingThatReferencesThem()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (expired, _) = await AddGuestAsync(context, sessions: 3, createdDaysAgo: 20);
        var (active, _) = await AddGuestAsync(context, sessions: 1);
        var (insideGrace, _) = await AddGuestAsync(context, sessions: 1, createdDaysAgo: 10);
        var storage = new FakeObjectStorage();
        var expiredKeys = await context.MediaUploadSessions
            .Where(s => s.GuestId == expired.Id)
            .Select(s => s.ObjectKey)
            .ToListAsync();

        var result = await NewCleanupService(context, storage).CleanupAsync();

        Assert.True(result.GuestsDeleted >= 1);
        await using var verify = _fixture.CreateContext();
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == expired.Id));
        Assert.False(await verify.MediaUploadSessions.AnyAsync(s => s.GuestId == expired.Id));
        Assert.All(expiredKeys, key => Assert.Contains(key, storage.Deleted));
        // Active and in-grace guests are untouched, sessions included.
        Assert.True(await verify.Guests.AnyAsync(g => g.Id == active.Id));
        Assert.True(await verify.Guests.AnyAsync(g => g.Id == insideGrace.Id));
        Assert.Equal(
            MediaUploadStatus.Pending,
            (await verify.MediaUploadSessions.SingleAsync(s => s.GuestId == insideGrace.Id)).Status);
    }

    [SkippableFact]
    public async Task Cleanup_KeepsTheGuestAndRespectsTheForeignKey_WhileStorageIsDown()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (guest, _) = await AddGuestAsync(context, sessions: 2, createdDaysAgo: 20);
        var storage = new FakeObjectStorage { FailDeletes = true };

        var first = await NewCleanupService(context, storage).CleanupAsync();

        Assert.Equal(0, first.GuestsFailed);
        await using (var verify = _fixture.CreateContext())
        {
            Assert.True(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
            // Sessions were invalidated up front even though the objects could not be purged yet.
            Assert.All(
                await verify.MediaUploadSessions.Where(s => s.GuestId == guest.Id).ToListAsync(),
                s => Assert.Equal(MediaUploadStatus.Expired, s.Status));
        }

        storage.FailDeletes = false;
        await using var retryContext = _fixture.CreateContext();
        await NewCleanupService(retryContext, storage).CleanupAsync();

        await using var final = _fixture.CreateContext();
        Assert.False(await final.Guests.AnyAsync(g => g.Id == guest.Id));
        Assert.False(await final.MediaUploadSessions.AnyAsync(s => s.GuestId == guest.Id));
    }

    [SkippableFact]
    public async Task Cleanup_DoesNotDeleteAGuestThatIsClaimedMeanwhile()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var (guest, raw) = await AddGuestAsync(context, sessions: 1);

        // A valid guest is claimed; a later cleanup pass finds nothing to purge for it.
        Assert.Equal(GuestClaimResult.Claimed, await NewClaimService(context).ClaimAndDeleteGuestAsync(raw, user.Id));
        await using var cleanupContext = _fixture.CreateContext();
        await NewCleanupService(cleanupContext, new FakeObjectStorage()).CleanupAsync();

        await using var verify = _fixture.CreateContext();
        Assert.Equal(1, await verify.MediaUploadSessions.CountAsync(s => s.UserId == user.Id));
        Assert.False(await verify.Guests.AnyAsync(g => g.Id == guest.Id));
    }

    private static GuestMediaClaimService NewClaimService(YakkuDbContext context)
    {
        return new GuestMediaClaimService(
            new GuestRepository(context),
            Hasher,
            new MediaUploadSessionRepository(context),
            new DeviceRepository(context),
            new RecordingAuditLogWriter());
    }

    private static GuestCleanupService NewCleanupService(YakkuDbContext context, FakeObjectStorage storage)
    {
        var sessions = new MediaUploadSessionRepository(context);
        return new GuestCleanupService(
            new GuestRepository(context),
            sessions,
            new MediaUploadCleanupService(sessions, storage),
            GuestCleanupOptions.Default,
            new RecordingAuditLogWriter());
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var phone = "+1" + string.Concat(suffix.Where(char.IsDigit).Concat("0123456789").Take(10));
        var user = new User(phone, "Guest Tester", "anon-" + suffix);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(Guest Guest, string RawToken)> AddGuestAsync(
        YakkuDbContext context,
        int sessions,
        int createdDaysAgo = 0)
    {
        var raw = "token-" + Guid.NewGuid().ToString("N");
        var guest = new Guest(Hasher.Hash(raw), DateTime.UtcNow.AddDays(-createdDaysAgo));
        context.Guests.Add(guest);
        for (var i = 0; i < sessions; i++)
        {
            context.MediaUploadSessions.Add(MediaUploadSession.ForGuest(
                guest.Id,
                PollMediaType.Image,
                "image-bucket",
                ".jpg",
                "image/jpeg",
                3,
                i + 1,
                DateTime.UtcNow.AddHours(24)));
        }

        await context.SaveChangesAsync();
        return (guest, raw);
    }

    /// <summary>Delegates to the real repository but fails the save that would delete the guest.</summary>
    private sealed class InstrumentedGuestRepository : IGuestRepository
    {
        private readonly IGuestRepository _inner;

        public InstrumentedGuestRepository(IGuestRepository inner, bool failSave = true, TimeSpan? holdAfterLoad = null)
        {
            _inner = inner;
            _failSave = failSave;
            _holdAfterLoad = holdAfterLoad ?? TimeSpan.Zero;
        }

        private readonly bool _failSave;
        private readonly TimeSpan _holdAfterLoad;

        public bool SaveWasAttempted { get; private set; }

        public Task AddAsync(Guest guest, CancellationToken cancellationToken = default)
            => _inner.AddAsync(guest, cancellationToken);

        public Task<Guest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _inner.GetByIdAsync(id, cancellationToken);

        public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
            => _inner.GetByTokenHashAsync(tokenHash, cancellationToken);

        public async Task<Guest?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            var guest = await _inner.GetByTokenHashForUpdateAsync(tokenHash, cancellationToken);
            if (guest is not null && _holdAfterLoad > TimeSpan.Zero)
            {
                // Widens the race window: everyone who can see the guest has loaded it before anyone writes.
                await Task.Delay(_holdAfterLoad, cancellationToken);
            }

            return guest;
        }

        public Task<IReadOnlyList<Guest>> ListAsync(bool? active, DateTime utcNow, CancellationToken cancellationToken = default)
            => _inner.ListAsync(active, utcNow, cancellationToken);

        public Task<IReadOnlyList<Guid>> ListExpiredIdsAsync(DateTime expiredBeforeUtc, int batchSize, CancellationToken cancellationToken = default)
            => _inner.ListExpiredIdsAsync(expiredBeforeUtc, batchSize, cancellationToken);

        public Task<bool> DeleteExpiredByIdAsync(Guid guestId, DateTime expiredBeforeUtc, CancellationToken cancellationToken = default)
            => _inner.DeleteExpiredByIdAsync(guestId, expiredBeforeUtc, cancellationToken);

        public void Delete(Guest guest) => _inner.Delete(guest);

        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            => _inner.ExecuteInTransactionAsync(action, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveWasAttempted = true;
            if (_failSave)
            {
                throw new InvalidOperationException("Simulated failure while deleting the guest.");
            }

            return _inner.SaveChangesAsync(cancellationToken);
        }
    }
}
