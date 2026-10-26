using Yakku.Application.Common.Exceptions;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Services;
using Yakku.Application.Polls.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class GuestMediaUploadTests
{
    private static readonly byte[] Jpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01
    ];

    [Fact]
    public async Task Guest_CanCreateCompleteGetAndCancelOwnSession()
    {
        var harness = new GuestHarness();
        var session = await harness.CreateGuestSessionAsync();
        Assert.Equal("pending", session.Status);
        Assert.Contains("/guests/", session.ObjectKey, StringComparison.Ordinal);

        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        var completed = await harness.Sessions.CompleteAsync(
            session.SessionId,
            harness.GuestOwner,
            new CompleteMediaUploadRequest { SelectionVersion = 1 });
        Assert.Equal("uploaded", completed.Status);

        var status = await harness.Sessions.GetAsync(session.SessionId, harness.GuestOwner);
        Assert.Equal("uploaded", status.Status);

        await harness.Sessions.CancelAsync(session.SessionId, harness.GuestOwner);
        var cancelled = await harness.SessionStore.GetByIdAsync(session.SessionId);
        Assert.Equal(MediaUploadStatus.Cancelled, cancelled!.Status);
    }

    [Fact]
    public async Task Guest_CannotAccessAnotherGuestsSession()
    {
        var owner = new GuestHarness();
        var other = new GuestHarness();
        var session = await owner.CreateGuestSessionAsync();

        var denied = await Assert.ThrowsAsync<AppException>(() =>
            other.Sessions.GetAsync(session.SessionId, other.GuestOwner));
        Assert.Equal(404, denied.StatusCode);
    }

    [Fact]
    public async Task JwtUser_UploadFlowStillWorks()
    {
        var harness = new GuestHarness();
        var session = await harness.Sessions.CreateAsync(
            new CreateMediaUploadSessionRequest
            {
                MediaType = "image",
                ContentType = "image/jpeg",
                FileSize = Jpeg.Length,
                SelectionVersion = 1
            },
            MediaOwner.ForUser(harness.UserId));

        Assert.Contains("/users/", session.ObjectKey, StringComparison.Ordinal);
        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        var completed = await harness.Sessions.CompleteAsync(
            session.SessionId,
            MediaOwner.ForUser(harness.UserId),
            new CompleteMediaUploadRequest { SelectionVersion = 1 });
        Assert.Equal("uploaded", completed.Status);
    }

    [Fact]
    public async Task VerifyClaim_TransfersSessionsDeletesGuest_ThenCreatePollSucceeds()
    {
        var harness = new GuestHarness();
        var session = await harness.CreateGuestSessionAsync();
        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        await harness.Sessions.CompleteAsync(
            session.SessionId,
            harness.GuestOwner,
            new CompleteMediaUploadRequest { SelectionVersion = 1 });

        await harness.Claim.ClaimAndDeleteGuestAsync(harness.RawToken, harness.UserId);

        var stored = await harness.SessionStore.GetByIdAsync(session.SessionId);
        Assert.Equal(harness.UserId, stored!.UserId);
        Assert.Null(stored.GuestId);
        Assert.Null(await harness.Guests.GetByTokenHashAsync(harness.TokenHash));

        var request = new CreatePollRequest
        {
            Question = "Guest upload poll?",
            ContentType = "image",
            MediaUploadSessionId = session.SessionId,
            Options =
            [
                new CreatePollOptionRequest { Text = "A", DisplayOrder = 1 },
                new CreatePollOptionRequest { Text = "B", DisplayOrder = 2 }
            ]
        };
        var poll = await harness.Polls.CreateAsync(request, harness.UserId);
        Assert.Equal("active", poll.Status);
        Assert.Equal("ready", poll.MediaReadiness);
    }

    [Fact]
    public async Task Claim_WithoutGuestToken_IsNoOp()
    {
        var harness = new GuestHarness();
        await harness.Claim.ClaimAndDeleteGuestAsync(null, harness.UserId);
        await harness.Claim.ClaimAndDeleteGuestAsync("   ", harness.UserId);
        await harness.Claim.ClaimAndDeleteGuestAsync("unknown-token", harness.UserId);
    }

    [Fact]
    public async Task Guest_CannotLinkUploadToExistingPoll()
    {
        var harness = new GuestHarness();
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Sessions.CreateAsync(
                new CreateMediaUploadSessionRequest
                {
                    MediaType = "image",
                    ContentType = "image/jpeg",
                    FileSize = Jpeg.Length,
                    SelectionVersion = 1,
                    PollId = Guid.NewGuid()
                },
                harness.GuestOwner));
        Assert.Equal(409, exception.StatusCode);
    }

    private sealed class GuestHarness
    {
        public GuestHarness()
        {
            UserId = Guid.NewGuid();
            RawToken = "guest-raw-token-" + Guid.NewGuid().ToString("N");
            var hasher = new GuestTokenHasher();
            TokenHash = hasher.Hash(RawToken);
            var guest = new Guest(TokenHash);
            GuestId = guest.Id;
            GuestOwner = MediaOwner.ForGuest(GuestId);

            Guests = new InMemoryGuestRepository();
            Guests.Seed(guest);
            PollStore = new InMemoryPollRepository([]);
            SessionStore = new InMemorySessionRepository();
            Storage = new FakeObjectStorage();
            var policy = MediaUploadPolicy.Default;
            Sessions = new MediaUploadSessionService(
                SessionStore,
                PollStore,
                new FakeBuckets(),
                Storage,
                policy,
                new FakeMediaInspector(),
                new CreateMediaUploadSessionValidator(),
                new CompleteMediaUploadValidator());
            Polls = new PollService(
                PollStore,
                SessionStore,
                Storage,
                policy,
                new CreatePollValidator(),
                new UpdatePollValidator(),
                Audit,
                new NoOpStreakService());
            Claim = new GuestMediaClaimService(Guests, hasher, SessionStore, new InMemoryDeviceRepository(), new InMemoryGuestVoteClaims(), Audit);
        }

        public RecordingAuditLogWriter Audit { get; } = new();

        public Guid UserId { get; }
        public Guid GuestId { get; }
        public string RawToken { get; }
        public string TokenHash { get; }
        public MediaOwner GuestOwner { get; }
        public InMemoryGuestRepository Guests { get; }
        public InMemoryPollRepository PollStore { get; }
        public InMemorySessionRepository SessionStore { get; }
        public FakeObjectStorage Storage { get; }
        public MediaUploadSessionService Sessions { get; }
        public PollService Polls { get; }
        public GuestMediaClaimService Claim { get; }

        public Task<MediaUploadSessionResponse> CreateGuestSessionAsync()
        {
            return Sessions.CreateAsync(
                new CreateMediaUploadSessionRequest
                {
                    MediaType = "image",
                    ContentType = "image/jpeg",
                    FileSize = Jpeg.Length,
                    SelectionVersion = 1
                },
                GuestOwner);
        }
    }
}
