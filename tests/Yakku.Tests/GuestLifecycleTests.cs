using System.Text.Json;
using Yakku.Application.Audit;
using Yakku.Application.Guests;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class GuestLifecycleTests
{
    [Fact]
    public async Task Establish_ReusesAValidGuest_WithoutIssuingANewToken()
    {
        var h = new Harness();
        var guest = h.SeedGuest();

        var result = await h.Identity.EstablishAsync(h.RawToken);

        Assert.Equal(guest.Id, result.GuestId);
        Assert.Null(result.RawTokenToSet);
        Assert.Single(h.Guests.All);
        Assert.DoesNotContain(h.Audit.Authentication, entry => entry.EventType == AuditEvents.Auth.GuestCreated);
    }

    [Fact]
    public async Task Establish_WithoutToken_CreatesExactlyOneGuest_ThenReusesIt()
    {
        var h = new Harness();

        var first = await h.Identity.EstablishAsync(null);
        Assert.NotNull(first.RawTokenToSet);
        Assert.Single(h.Guests.All);

        var second = await h.Identity.EstablishAsync(first.RawTokenToSet);
        Assert.Equal(first.GuestId, second.GuestId);
        Assert.Null(second.RawTokenToSet);
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Establish_ToleratesWhitespaceAroundTheToken()
    {
        var h = new Harness();
        var guest = h.SeedGuest();

        var result = await h.Identity.EstablishAsync("  " + h.RawToken + "\n");

        Assert.Equal(guest.Id, result.GuestId);
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Establish_NeverRevivesAnExpiredGuest()
    {
        var h = new Harness();
        var expired = h.SeedGuest(createdDaysAgo: 20);

        var result = await h.Identity.EstablishAsync(h.RawToken);

        Assert.NotEqual(expired.Id, result.GuestId);
        Assert.NotNull(result.RawTokenToSet);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal(2, h.Guests.All.Count);
        Assert.Contains(h.Audit.Authentication, entry =>
            entry.EventType == AuditEvents.Auth.GuestTokenExpired && entry.ActorId == expired.Id);
        Assert.Contains(h.Audit.Authentication, entry => entry.EventType == AuditEvents.Auth.GuestCreated);
    }

    [Fact]
    public async Task Establish_WithUnknownToken_CreatesANewGuest()
    {
        var h = new Harness();

        var result = await h.Identity.EstablishAsync("not-a-known-token");

        Assert.NotNull(result.RawTokenToSet);
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Claim_TransfersSessionsAndDeletesTheGuest_WithoutLoggingTheToken()
    {
        var h = new Harness();
        var guest = h.SeedGuest();
        var session = await h.AddGuestSessionAsync(guest);

        var result = await h.Claim.ClaimAndDeleteGuestAsync(h.RawToken, h.UserId);

        Assert.Equal(GuestClaimResult.Claimed, result);
        Assert.Empty(h.Guests.All);
        var stored = await h.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(h.UserId, stored!.UserId);
        Assert.Null(stored.GuestId);

        var audit = Assert.Single(h.Audit.Authentication, entry => entry.EventType == AuditEvents.Auth.GuestClaimed);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal("1", audit.Metadata!["sessionsTransferred"]);
        Assert.DoesNotContain(h.RawToken, JsonSerializer.Serialize(h.Audit.Authentication));
    }

    [Fact]
    public async Task Claim_NeverClaimsAnExpiredGuest()
    {
        var h = new Harness();
        var guest = h.SeedGuest(createdDaysAgo: 20);
        var session = await h.AddGuestSessionAsync(guest);

        var result = await h.Claim.ClaimAndDeleteGuestAsync(h.RawToken, h.UserId);

        Assert.Equal(GuestClaimResult.Expired, result);
        Assert.Single(h.Guests.All);
        var stored = await h.Sessions.GetByIdAsync(session.Id);
        Assert.Equal(guest.Id, stored!.GuestId);
        Assert.Null(stored.UserId);
        var rejected = Assert.Single(h.Audit.Authentication, entry => entry.EventType == AuditEvents.Auth.GuestClaimRejected);
        Assert.Equal("guest_expired", rejected.FailureReason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Claim_WithoutAToken_ReportsNoToken(string? token)
    {
        var h = new Harness();
        h.SeedGuest();

        Assert.Equal(GuestClaimResult.NoToken, await h.Claim.ClaimAndDeleteGuestAsync(token, h.UserId));
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Claim_WithAnUnknownToken_ReportsNotFound_AndLeavesEverythingAlone()
    {
        var h = new Harness();
        h.SeedGuest();

        Assert.Equal(GuestClaimResult.NotFound, await h.Claim.ClaimAndDeleteGuestAsync("unknown", h.UserId));
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Claim_AfterTheGuestWasClaimed_IsNotADuplicate()
    {
        var h = new Harness();
        var guest = h.SeedGuest();
        await h.AddGuestSessionAsync(guest);

        Assert.Equal(GuestClaimResult.Claimed, await h.Claim.ClaimAndDeleteGuestAsync(h.RawToken, h.UserId));
        Assert.Equal(GuestClaimResult.NotFound, await h.Claim.ClaimAndDeleteGuestAsync(h.RawToken, Guid.NewGuid()));
    }

    [Fact]
    public async Task ClaimAndDelete_ThrowsWhenTheDatabaseFails_AndKeepsTheGuest()
    {
        var h = new Harness();
        h.SeedGuest();
        h.Guests.ThrowOnSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.Claim.ClaimAndDeleteGuestAsync(h.RawToken, h.UserId));

        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task TryClaim_NeverThrows_ReportsFailed_AndAuditsOnlyTheExceptionType()
    {
        var h = new Harness();
        h.SeedGuest();
        h.Guests.ThrowOnSave = true;

        var result = await h.Claim.TryClaimAsync(h.RawToken, h.UserId);

        Assert.Equal(GuestClaimResult.Failed, result);
        Assert.Single(h.Guests.All);
        var failure = Assert.Single(h.Audit.Authentication, entry => entry.EventType == AuditEvents.Auth.GuestClaimFailed);
        Assert.Equal(AuditOutcome.Failure, failure.Outcome);
        Assert.Equal(nameof(InvalidOperationException), failure.FailureReason);
        Assert.DoesNotContain(h.RawToken, JsonSerializer.Serialize(h.Audit.Authentication));
    }

    [Fact]
    public async Task TryClaim_WithAValidGuest_BehavesLikeClaim()
    {
        var h = new Harness();
        h.SeedGuest();

        Assert.Equal(GuestClaimResult.Claimed, await h.Claim.TryClaimAsync(h.RawToken, h.UserId));
        Assert.Empty(h.Guests.All);
    }

    [Fact]
    public async Task Cleanup_PurgesGuestsPastTheGracePeriod_InvalidatingSessionsFirst()
    {
        var h = new Harness();
        var guest = h.SeedGuest(createdDaysAgo: 20);
        var pending = await h.AddGuestSessionAsync(guest);
        var uploaded = await h.AddGuestSessionAsync(guest);
        h.Storage.Put(uploaded.ObjectKey, [1, 2, 3], "image/jpeg");

        var result = await h.Cleanup.CleanupAsync();

        Assert.Equal(new GuestCleanupResult(1, 2, 1, 0, 0), result);
        Assert.Empty(h.Guests.All);
        Assert.Equal(0, await h.Sessions.CountGuestSessionsAsync(guest.Id));
        Assert.Contains(pending.ObjectKey, h.Storage.Deleted);
        Assert.Contains(uploaded.ObjectKey, h.Storage.Deleted);
        var summary = Assert.Single(h.Audit.System);
        Assert.Equal(SystemLogCategory.BackgroundJob, summary.Category);
        Assert.Equal("1", summary.Metadata!["guestsDeleted"]);
    }

    [Fact]
    public async Task Cleanup_LeavesGuestsInsideTheGracePeriod()
    {
        var h = new Harness();
        // Expired 3 days ago, grace is 7 days: not eligible yet.
        var guest = h.SeedGuest(createdDaysAgo: 10);
        var session = await h.AddGuestSessionAsync(guest);

        var result = await h.Cleanup.CleanupAsync();

        Assert.Equal(new GuestCleanupResult(0, 0, 0, 0, 0), result);
        Assert.Single(h.Guests.All);
        Assert.Equal(MediaUploadStatus.Pending, (await h.Sessions.GetByIdAsync(session.Id))!.Status);
        Assert.Empty(h.Storage.Deleted);
    }

    [Fact]
    public async Task Cleanup_NeverTouchesAnActiveGuest()
    {
        var h = new Harness();
        h.SeedGuest();

        var result = await h.Cleanup.CleanupAsync();

        Assert.Equal(0, result.Examined);
        Assert.Single(h.Guests.All);
    }

    [Fact]
    public async Task Cleanup_DefersTheGuestWhileStorageCannotBePurged_ThenFinishesLater()
    {
        var h = new Harness();
        var guest = h.SeedGuest(createdDaysAgo: 20);
        var session = await h.AddGuestSessionAsync(guest);
        h.Storage.FailDeletes = true;

        var first = await h.Cleanup.CleanupAsync();

        // Sessions are invalidated right away, but the guest must stay while its object is still in storage.
        Assert.Equal(1, first.GuestsDeferred);
        Assert.Equal(0, first.GuestsDeleted);
        Assert.Single(h.Guests.All);
        Assert.Equal(MediaUploadStatus.Expired, (await h.Sessions.GetByIdAsync(session.Id))!.Status);

        h.Storage.FailDeletes = false;
        var second = await h.Cleanup.CleanupAsync();

        Assert.Equal(1, second.GuestsDeleted);
        Assert.Empty(h.Guests.All);
        Assert.Contains(session.ObjectKey, h.Storage.Deleted);
    }

    private sealed class Harness
    {
        public Harness()
        {
            UserId = Guid.NewGuid();
            RawToken = "guest-raw-token-" + Guid.NewGuid().ToString("N");
            Hasher = new GuestTokenHasher();
            Guests = new InMemoryGuestRepository();
            Sessions = new InMemorySessionRepository();
            Storage = new FakeObjectStorage();
            Audit = new RecordingAuditLogWriter();
            Identity = new GuestIdentityService(Guests, new GuestTokenGenerator(), Hasher, Audit);
            Claim = new GuestMediaClaimService(Guests, Hasher, Sessions, new InMemoryDeviceRepository(), Audit);
            Cleanup = new GuestCleanupService(
                Guests,
                Sessions,
                new MediaUploadCleanupService(Sessions, Storage),
                GuestCleanupOptions.Default,
                Audit);
        }

        public Guid UserId { get; }

        public string RawToken { get; }

        public GuestTokenHasher Hasher { get; }

        public InMemoryGuestRepository Guests { get; }

        public InMemorySessionRepository Sessions { get; }

        public FakeObjectStorage Storage { get; }

        public RecordingAuditLogWriter Audit { get; }

        public GuestIdentityService Identity { get; }

        public GuestMediaClaimService Claim { get; }

        public GuestCleanupService Cleanup { get; }

        public Guest SeedGuest(int createdDaysAgo = 0)
        {
            var guest = new Guest(Hasher.Hash(RawToken), DateTime.UtcNow.AddDays(-createdDaysAgo));
            Guests.Seed(guest);
            return guest;
        }

        public async Task<MediaUploadSession> AddGuestSessionAsync(Guest guest)
        {
            var session = MediaUploadSession.ForGuest(
                guest.Id,
                PollMediaType.Image,
                "image-bucket",
                ".jpg",
                "image/jpeg",
                3,
                1,
                DateTime.UtcNow.AddHours(24));
            await Sessions.AddAsync(session);
            return session;
        }
    }
}
