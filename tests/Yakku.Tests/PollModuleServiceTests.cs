using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Polls;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Services;
using Yakku.Application.Polls.Validators;
using Yakku.Application.Votes.DTOs;
using Yakku.Application.Votes.Services;
using Yakku.Application.Votes.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class PollModuleServiceTests
{
    private static readonly byte[] Jpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01
    ];

    private static readonly byte[] Mp4 =
    [
        0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70, 0x6D, 0x70, 0x34, 0x32
    ];

    private static readonly byte[] Webm =
    [
        0x1A, 0x45, 0xDF, 0xA3, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ];

    [Fact]
    public async Task CreateQuestionPoll_IsActiveWithoutMedia()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);

        Assert.Equal("active", created.Status);
        Assert.Equal("none", created.MediaReadiness);
        Assert.Equal(2, created.Options.Count);
        Assert.Null(created.Media);
    }

    [Fact]
    public async Task CreatePoll_RejectsFewerThanTwoOptions()
    {
        var harness = new PollHarness();
        var request = QuestionRequest();
        request.Options = [new CreatePollOptionRequest { Text = "Only one" }];

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.Polls.CreateAsync(request, harness.UserId));

        Assert.Contains("at least 2", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePoll_RejectsMoreThanThreeOptions()
    {
        var harness = new PollHarness();
        var request = QuestionRequest();
        request.Options =
        [
            new CreatePollOptionRequest { Text = "A" },
            new CreatePollOptionRequest { Text = "B" },
            new CreatePollOptionRequest { Text = "C" },
            new CreatePollOptionRequest { Text = "D" }
        ];

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.Polls.CreateAsync(request, harness.UserId));

        Assert.Contains("at most 3", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("video")]
    [InlineData("audio")]
    public async Task CreateMediaPoll_WithoutUpload_IsRejected(string contentType)
    {
        var harness = new PollHarness();
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            harness.Polls.CreateAsync(QuestionRequest(contentType), harness.UserId));

        Assert.Contains("media upload", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateMediaPoll_RejectsPendingUpload()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        var request = QuestionRequest("image");
        request.MediaUploadSessionId = session.SessionId;

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Polls.CreateAsync(request, harness.UserId));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task CreateMediaPoll_IsActiveWithReadyMedia()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        await harness.Sessions.CompleteAsync(
            session.SessionId,
            MediaOwner.ForUser(harness.UserId),
            new CompleteMediaUploadRequest { SelectionVersion = 1 });

        var request = QuestionRequest("image");
        request.MediaUploadSessionId = session.SessionId;
        var created = await harness.Polls.CreateAsync(request, harness.UserId);

        Assert.Equal("active", created.Status);
        Assert.Equal("ready", created.MediaReadiness);
        Assert.NotNull(created.Media);
    }

    [Fact]
    public async Task CreatePoll_RejectsMismatchedMediaType()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("video", "video/mp4", 2);

        var request = QuestionRequest("image");
        request.MediaUploadSessionId = session.SessionId;

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Polls.CreateAsync(request, harness.UserId));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task ReplacingMedia_DoesNotAttachObsoleteUpload()
    {
        var harness = new PollHarness();
        var first = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        var second = await harness.CreateSessionAsync("image", "image/jpeg", 2);
        harness.Storage.Put(second.ObjectKey, Jpeg, "image/jpeg");
        await harness.Sessions.CompleteAsync(second.SessionId, MediaOwner.ForUser(harness.UserId), new CompleteMediaUploadRequest { SelectionVersion = 2 });

        var obsolete = QuestionRequest("image");
        obsolete.MediaUploadSessionId = first.SessionId;
        var rejected = await Assert.ThrowsAsync<AppException>(() => harness.Polls.CreateAsync(obsolete, harness.UserId));
        Assert.Equal(409, rejected.StatusCode);

        var current = QuestionRequest("image");
        current.MediaUploadSessionId = second.SessionId;
        var created = await harness.Polls.CreateAsync(current, harness.UserId);

        Assert.Equal("ready", created.MediaReadiness);
        Assert.Equal(second.ObjectKey, harness.PollStore.Polls.Single().Media!.ObjectKey);
    }

    [Fact]
    public async Task LateCompletion_CannotReplaceCurrentSelection()
    {
        var harness = new PollHarness();
        var first = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        var second = await harness.CreateSessionAsync("image", "image/jpeg", 2);
        harness.Storage.Put(first.ObjectKey, Jpeg, "image/jpeg");
        harness.Storage.Put(second.ObjectKey, Jpeg, "image/jpeg");
        await harness.Sessions.CompleteAsync(second.SessionId, MediaOwner.ForUser(harness.UserId), new CompleteMediaUploadRequest { SelectionVersion = 2 });

        var request = QuestionRequest("image");
        request.MediaUploadSessionId = second.SessionId;
        var created = await harness.Polls.CreateAsync(request, harness.UserId);

        var late = await Assert.ThrowsAsync<AppException>(() =>
            harness.Sessions.CompleteAsync(first.SessionId, MediaOwner.ForUser(harness.UserId), new CompleteMediaUploadRequest { SelectionVersion = 1 }));

        Assert.Equal(409, late.StatusCode);
        Assert.Equal(second.ObjectKey, harness.PollStore.Polls.Single(poll => poll.Id == created.Id).Media!.ObjectKey);
    }

    [Fact]
    public async Task CompleteUpload_IsIdempotent()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        var request = new CompleteMediaUploadRequest { SelectionVersion = 1 };

        var first = await harness.Sessions.CompleteAsync(session.SessionId, MediaOwner.ForUser(harness.UserId), request);
        var second = await harness.Sessions.CompleteAsync(session.SessionId, MediaOwner.ForUser(harness.UserId), request);

        Assert.Equal("uploaded", first.Status);
        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal("uploaded", second.Status);
        Assert.Equal(1, harness.Storage.HeadCalls);
    }

    [Fact]
    public async Task AttachUpload_RejectsAnotherUsersSession()
    {
        var owner = new PollHarness();
        var other = new PollHarness();
        var session = await owner.CreateSessionAsync("image", "image/jpeg", 1);
        owner.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        await owner.Sessions.CompleteAsync(session.SessionId, MediaOwner.ForUser(owner.UserId), new CompleteMediaUploadRequest { SelectionVersion = 1 });

        var request = QuestionRequest("image");
        request.MediaUploadSessionId = session.SessionId;
        var exception = await Assert.ThrowsAsync<AppException>(() => other.Polls.CreateAsync(request, other.UserId));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Publish_RejectsAlreadyActivePoll()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Polls.PublishAsync(created.Id, harness.UserId));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Publish_RejectsQuestionPollWithMedia()
    {
        var poll = new Poll(Guid.NewGuid(), "Which?", PollContentType.Question);
        poll.AddTextOption("A", 1);
        poll.AddTextOption("B", 2);
        var media = new PollMedia(poll.Id, PollMediaType.Image, "image-bucket", "polls/pending/file.jpg", "image/jpeg", 12, null, null);

        var exception = Assert.Throws<InvalidOperationException>(() => poll.AttachMedia(media));

        Assert.Contains("Question polls cannot have media", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_RejectsIncompatibleMedia()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("video", "video/mp4", 1);
        harness.Storage.Put(session.ObjectKey, Mp4, "video/mp4");
        await harness.Sessions.CompleteAsync(session.SessionId, MediaOwner.ForUser(harness.UserId), new CompleteMediaUploadRequest { SelectionVersion = 1 });

        var request = QuestionRequest("audio");
        request.MediaUploadSessionId = session.SessionId;
        var exception = await Assert.ThrowsAsync<AppException>(() => harness.Polls.CreateAsync(request, harness.UserId));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Delete_IsLimitedToTheCreator()
    {
        var owner = new PollHarness();
        var other = new PollHarness();
        var created = await owner.Polls.CreateAsync(QuestionRequest(), owner.UserId);

        var delete = await Assert.ThrowsAsync<AppException>(() => other.Polls.DeletePollAsync(created.Id, other.UserId));
        Assert.Equal(404, delete.StatusCode);

        await owner.Polls.DeletePollAsync(created.Id, owner.UserId);
        Assert.Empty(owner.PollStore.Polls);
    }

    [Fact]
    public async Task Vote_AllowsOneVotePerUser()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);
        var optionId = created.Options[0].Id;
        var voter = Guid.NewGuid();

        await harness.Votes.CastAsync(created.Id, voter, new CastVoteRequest { PollId = created.Id, PollOptionId = optionId }, CancellationToken.None);
        var duplicate = await Assert.ThrowsAsync<AppException>(() =>
            harness.Votes.CastAsync(created.Id, voter, new CastVoteRequest { PollId = created.Id, PollOptionId = optionId }, CancellationToken.None));

        Assert.Equal(409, duplicate.StatusCode);
    }

    [Fact]
    public async Task Vote_RejectsOptionFromAnotherPoll()
    {
        var harness = new PollHarness();
        var first = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);
        var second = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Votes.CastAsync(
                first.Id,
                Guid.NewGuid(),
                new CastVoteRequest { PollId = first.Id, PollOptionId = second.Options[0].Id },
                CancellationToken.None));

        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task Vote_RequiresExactlyOneAnswer()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);
        var voter = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            harness.Votes.CastAsync(created.Id, voter, new CastVoteRequest { PollId = created.Id }, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            harness.Votes.CastAsync(
                created.Id,
                voter,
                new CastVoteRequest
                {
                    PollId = created.Id,
                    PollOptionId = created.Options[0].Id,
                    CustomAnswer = "Both"
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task Vote_AllowsReasonOnEitherAnswer()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);

        var optionVote = await harness.Votes.CastAsync(
            created.Id,
            Guid.NewGuid(),
            new CastVoteRequest { PollId = created.Id, PollOptionId = created.Options[0].Id, Reason = "I prefer this option." },
            CancellationToken.None);
        var customVote = await harness.Votes.CastAsync(
            created.Id,
            Guid.NewGuid(),
            new CastVoteRequest { PollId = created.Id, CustomAnswer = "Rust", Reason = "I prefer Rust for this use case." },
            CancellationToken.None);

        Assert.Equal("I prefer this option.", optionVote.Reason);
        Assert.Equal("I prefer Rust for this use case.", customVote.Reason);
        Assert.Equal("Rust", customVote.CustomAnswer);
    }

    [Fact]
    public async Task Results_AggregateOptionAndCustomVotes()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);
        await harness.Votes.CastAsync(created.Id, Guid.NewGuid(), new CastVoteRequest { PollId = created.Id, PollOptionId = created.Options[0].Id, Reason = "Because" }, CancellationToken.None);
        await harness.Votes.CastAsync(created.Id, Guid.NewGuid(), new CastVoteRequest { PollId = created.Id, CustomAnswer = "Rust" }, CancellationToken.None);

        var results = await harness.Polls.GetResultsAsync(created.Id, harness.UserId);

        Assert.Equal(2, results.TotalVoteCount);
        Assert.Equal(1, results.CustomAnswerCount);
        Assert.Equal(1, results.TotalWhyCount);
        Assert.Equal(1, results.Options.Single(option => option.Id == created.Options[0].Id).VoteCount);
    }

    [Fact]
    public async Task Cleanup_DoesNotDeleteReferencedMedia()
    {
        var harness = new PollHarness();
        var referenced = MediaUploadSession.ForUser(
            harness.UserId,
            null,
            PollMediaType.Image,
            "image-bucket",
            ".jpg",
            "image/jpeg",
            Jpeg.Length,
            1,
            DateTime.UtcNow.AddMinutes(-5));
        var orphan = MediaUploadSession.ForUser(
            harness.UserId,
            null,
            PollMediaType.Image,
            "image-bucket",
            ".jpg",
            "image/jpeg",
            Jpeg.Length,
            2,
            DateTime.UtcNow.AddMinutes(-5));
        await harness.SessionStore.AddAsync(referenced);
        await harness.SessionStore.AddAsync(orphan);
        harness.SessionStore.IsReferenced = (bucket, key) => key == referenced.ObjectKey;

        await harness.Cleanup.CleanupAsync();

        Assert.DoesNotContain(referenced.ObjectKey, harness.Storage.Deleted);
        Assert.Contains(orphan.ObjectKey, harness.Storage.Deleted);
    }

    [Fact]
    public async Task CompleteUpload_KeepsSessionWhenDatabaseUpdateFails()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        harness.SessionStore.ThrowOnUpload = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Sessions.CompleteAsync(session.SessionId, MediaOwner.ForUser(harness.UserId), new CompleteMediaUploadRequest { SelectionVersion = 1 }));

        var stored = await harness.SessionStore.GetByIdAsync(session.SessionId);
        Assert.Equal(MediaUploadStatus.Pending, stored!.Status);
        Assert.Equal(session.ObjectKey, stored.ObjectKey);

        harness.SessionStore.ThrowOnUpload = false;
        var completed = await harness.Sessions.CompleteAsync(
            session.SessionId,
            MediaOwner.ForUser(harness.UserId),
            new CompleteMediaUploadRequest { SelectionVersion = 1 });
        Assert.Equal("uploaded", completed.Status);
    }

    [Fact]
    public void MediaSignature_RejectsMismatchedBytes()
    {
        Assert.False(MediaSignature.Matches(PollMediaType.Image, "image/jpeg", Mp4));
        Assert.True(MediaSignature.Matches(PollMediaType.Video, "video/mp4", Mp4));
        Assert.True(MediaSignature.Matches(PollMediaType.Video, "video/webm", Webm));
        Assert.False(PollMediaCatalog.TryNormalize(PollMediaType.Image, "image/gif", out _));
        Assert.False(PollMediaCatalog.TryNormalize(PollMediaType.Video, "video/quicktime", out _));
        Assert.Equal(20 * 1024 * 1024, MediaUploadPolicy.Default.MaxBytes(PollMediaType.Audio));
        Assert.Equal(TimeSpan.FromSeconds(60), MediaUploadPolicy.Default.MaxDuration(PollMediaType.Video));
        Assert.Equal(TimeSpan.FromSeconds(120), MediaUploadPolicy.Default.MaxDuration(PollMediaType.Audio));
    }

    [Fact]
    public async Task GetUpload_ReturnsOwnerStatusOnly()
    {
        var owner = new PollHarness();
        var other = new PollHarness();
        var session = await owner.CreateSessionAsync("image", "image/jpeg", 1);

        var status = await owner.Sessions.GetAsync(session.SessionId, MediaOwner.ForUser(owner.UserId));
        Assert.Equal("pending", status.Status);

        var denied = await Assert.ThrowsAsync<AppException>(() =>
            other.Sessions.GetAsync(session.SessionId, MediaOwner.ForUser(other.UserId)));
        Assert.Equal(404, denied.StatusCode);
    }

    [Fact]
    public async Task GetAccess_RequiresUploadedMedia()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);

        var pending = await Assert.ThrowsAsync<AppException>(() =>
            harness.Sessions.GetAccessAsync(session.SessionId, MediaOwner.ForUser(harness.UserId)));
        Assert.Equal(409, pending.StatusCode);

        harness.Storage.Put(session.ObjectKey, Jpeg, "image/jpeg");
        await harness.Sessions.CompleteAsync(
            session.SessionId,
            MediaOwner.ForUser(harness.UserId),
            new CompleteMediaUploadRequest { SelectionVersion = 1 });

        var access = await harness.Sessions.GetAccessAsync(session.SessionId, MediaOwner.ForUser(harness.UserId));
        Assert.False(string.IsNullOrWhiteSpace(access.Url));
    }

    [Fact]
    public async Task CompleteVideo_RejectsExcessiveDuration()
    {
        var harness = new PollHarness();
        harness.Inspector.DurationMs = 61_000;
        var session = await harness.CreateSessionAsync("video", "video/mp4", 1);
        harness.Storage.Put(session.ObjectKey, Mp4, "video/mp4");

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Sessions.CompleteAsync(
                session.SessionId,
                MediaOwner.ForUser(harness.UserId),
                new CompleteMediaUploadRequest { SelectionVersion = 1 }));

        Assert.Equal(400, exception.StatusCode);
        var stored = await harness.SessionStore.GetByIdAsync(session.SessionId);
        Assert.Equal(MediaUploadStatus.Failed, stored!.Status);
        Assert.Contains(session.ObjectKey, harness.Storage.Deleted);
    }

    [Fact]
    public async Task CompleteVideo_PersistsDuration()
    {
        var harness = new PollHarness();
        harness.Inspector.DurationMs = 12_500;
        var session = await harness.CreateSessionAsync("video", "video/mp4", 1);
        harness.Storage.Put(session.ObjectKey, Mp4, "video/mp4");

        var completed = await harness.Sessions.CompleteAsync(
            session.SessionId,
            MediaOwner.ForUser(harness.UserId),
            new CompleteMediaUploadRequest { SelectionVersion = 1 });

        Assert.Equal(12_500, completed.DurationMs);

        var request = QuestionRequest("video");
        request.MediaUploadSessionId = session.SessionId;
        var poll = await harness.Polls.CreateAsync(request, harness.UserId);
        Assert.Equal(12_500, poll.Media!.DurationMs);
    }

    [Fact]
    public async Task Update_RejectsActivePoll()
    {
        var harness = new PollHarness();
        var created = await harness.Polls.CreateAsync(QuestionRequest(), harness.UserId);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.Polls.UpdateAsync(
                created.Id,
                new UpdatePollRequest { Question = "Updated question?" },
                harness.UserId));

        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task CreateUpload_EnforcesPendingQuota()
    {
        var harness = new PollHarness(policy: new MediaUploadPolicy(
            MediaUploadPolicy.DefaultImageMaxBytes,
            MediaUploadPolicy.DefaultAudioMaxBytes,
            MediaUploadPolicy.DefaultVideoMaxBytes,
            TimeSpan.FromSeconds(60),
            TimeSpan.FromSeconds(120),
            maxPendingPerUser: 1,
            maxConcurrentIncompletePerUser: 5,
            maxStorageBytesPerUser: MediaUploadPolicy.DefaultMaxStorageBytesPerUser,
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(15),
            TimeSpan.FromHours(24)));

        await harness.CreateSessionAsync("image", "image/jpeg", 1);
        var exception = await Assert.ThrowsAsync<AppException>(() =>
            harness.CreateSessionAsync("image", "image/jpeg", 2));
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Cleanup_PurgesFailedSessions()
    {
        var harness = new PollHarness();
        var session = await harness.CreateSessionAsync("image", "image/jpeg", 1);
        harness.Storage.Put(session.ObjectKey, Mp4, "image/jpeg");

        await Assert.ThrowsAsync<AppException>(() =>
            harness.Sessions.CompleteAsync(
                session.SessionId,
                MediaOwner.ForUser(harness.UserId),
                new CompleteMediaUploadRequest { SelectionVersion = 1 }));

        var stored = await harness.SessionStore.GetByIdAsync(session.SessionId);
        Assert.Equal(MediaUploadStatus.Failed, stored!.Status);
        Assert.Contains(session.ObjectKey, harness.Storage.Deleted);
    }

    private static CreatePollRequest QuestionRequest(string contentType = "question")
    {
        return new CreatePollRequest
        {
            Question = "Which presentation is better?",
            ContentType = contentType,
            Options =
            [
                new CreatePollOptionRequest { Text = "Option A", DisplayOrder = 1 },
                new CreatePollOptionRequest { Text = "Option B", DisplayOrder = 2 }
            ]
        };
    }

    private sealed class PollHarness
    {
        private readonly List<PollVote> _votes = [];

        public PollHarness(MediaUploadPolicy? policy = null)
        {
            UserId = Guid.NewGuid();
            PollStore = new InMemoryPollRepository(_votes);
            SessionStore = new InMemorySessionRepository();
            Storage = new FakeObjectStorage();
            Inspector = new FakeMediaInspector();
            policy ??= MediaUploadPolicy.Default;
            Polls = new PollService(
                PollStore,
                SessionStore,
                Storage,
                policy,
                new CreatePollValidator(),
                new UpdatePollValidator());
            Sessions = new MediaUploadSessionService(
                SessionStore,
                PollStore,
                new FakeBuckets(),
                Storage,
                policy,
                Inspector,
                new CreateMediaUploadSessionValidator(),
                new CompleteMediaUploadValidator());
            Votes = new VoteService(PollStore, new InMemoryVoteRepository(_votes), new CastVoteValidator());
            Cleanup = new MediaUploadCleanupService(SessionStore, Storage);
        }

        public Guid UserId { get; }

        public InMemoryPollRepository PollStore { get; }

        public InMemorySessionRepository SessionStore { get; }

        public FakeObjectStorage Storage { get; }

        public FakeMediaInspector Inspector { get; }

        public PollService Polls { get; }

        public MediaUploadSessionService Sessions { get; }

        public VoteService Votes { get; }

        public MediaUploadCleanupService Cleanup { get; }

        public Task<MediaUploadSessionResponse> CreateSessionAsync(string mediaType, string contentType, long version)
        {
            var bytes = contentType == "video/mp4" ? Mp4 : Jpeg;
            return Sessions.CreateAsync(
                new CreateMediaUploadSessionRequest
                {
                    MediaType = mediaType,
                    FileName = "advisory-name.bin",
                    ContentType = contentType,
                    FileSize = bytes.Length,
                    SelectionVersion = version
                }, MediaOwner.ForUser(UserId));
        }
    }
}
