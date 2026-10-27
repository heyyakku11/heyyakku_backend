using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Sharing.Services;
using Yakku.Application.Sharing.Validators;
using Yakku.Application.Votes;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class UnifiedVotingTests
{
    private static readonly Guid PollOwner = Guid.NewGuid();
    private static readonly Guid CheckOwner = Guid.NewGuid();

    [Fact]
    public async Task UserVote_OnAPoll_ReturnsThePollWithTheVotersChoice()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var voter = VoterIdentity.ForUser(Guid.NewGuid());

        var response = await h.Voting.VoteAsync(Request(poll, poll.Options[1].Id, reason: "  because  "), voter);

        Assert.Equal(ContentTypes.Poll, response.ContentType);
        Assert.Equal(poll.ContentId, response.ContentId);
        Assert.True(response.Voting.HasVoted);
        Assert.False(response.Voting.CanVote);
        Assert.Equal("ALREADY_VOTED", response.Voting.BlockedReason);
        Assert.Equal("because", response.Voting.MyVote!.Reason);
        Assert.Equal(poll.Options[1].Id, response.Voting.MyVote.OptionId);
        Assert.Single(response.Options, option => option.IsSelected);
        Assert.NotNull(response.Poll);
        Assert.Null(response.Check);
    }

    [Fact]
    public async Task GuestVote_OnACheck_IsAccepted_AndStoredWithTheGuestRole()
    {
        var h = new Harness();
        var check = h.AddCheck();
        var guest = VoterIdentity.ForGuest(Guid.NewGuid());

        var response = await h.Voting.VoteAsync(Request(check, check.Options[0].Id), guest);

        Assert.Equal(ContentTypes.Check, response.ContentType);
        Assert.Equal(check.Check!.CheckId, response.Check!.CheckId);
        Assert.True(response.Voting.HasVoted);
        var stored = Assert.Single(h.Repository.Votes);
        Assert.Equal(VoterRole.Guest, stored.Voter.Role);
    }

    [Fact]
    public async Task VotingByShareCode_ResolvesBothTypes()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var check = h.AddCheck();
        var voter = VoterIdentity.ForUser(Guid.NewGuid());

        var pollResponse = await h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, OptionId = poll.Options[0].Id }, voter);
        var checkResponse = await h.Voting.VoteAsync(
            new VoteRequest { ShareCode = check.ShareCode, OptionId = check.Options[0].Id }, voter);

        Assert.Equal(ContentTypes.Poll, pollResponse.ContentType);
        Assert.Equal(ContentTypes.Check, checkResponse.ContentType);
    }

    [Fact]
    public async Task CustomAnswer_IsTrimmed_AndMarksNoOptionSelected()
    {
        var h = new Harness();
        var poll = h.AddPoll();

        var response = await h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, CustomAnswer = "  Rust  " },
            VoterIdentity.ForUser(Guid.NewGuid()));

        Assert.Equal("Rust", response.Voting.MyVote!.CustomAnswer);
        Assert.DoesNotContain(response.Options, option => option.IsSelected);
    }

    [Theory]
    [InlineData(ShareContentType.Poll)]
    [InlineData(ShareContentType.Check)]
    public async Task SecondVote_ByTheSameVoter_Returns409(ShareContentType type)
    {
        var h = new Harness();
        var content = type == ShareContentType.Poll ? h.AddPoll() : h.AddCheck();
        var voter = VoterIdentity.ForGuest(Guid.NewGuid());
        await h.Voting.VoteAsync(Request(content, content.Options[0].Id), voter);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => h.Voting.VoteAsync(Request(content, content.Options[1].Id), voter));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.AlreadyVoted, exception.ErrorCode);
        Assert.Single(h.Repository.Votes);
    }

    [Fact]
    public async Task ACheckOwner_CannotVoteOnTheirOwnCheck_ButAPollOwnerCan()
    {
        var h = new Harness();
        var check = h.AddCheck();
        var poll = h.AddPoll();

        var forbidden = await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(
            Request(check, check.Options[0].Id), VoterIdentity.ForUser(CheckOwner)));
        var pollResponse = await h.Voting.VoteAsync(
            Request(poll, poll.Options[0].Id), VoterIdentity.ForUser(PollOwner));

        Assert.Equal(403, forbidden.StatusCode);
        Assert.True(pollResponse.IsOwner);
        Assert.True(pollResponse.Voting.HasVoted);
    }

    [Theory]
    [InlineData(ShareContentType.Poll)]
    [InlineData(ShareContentType.Check)]
    public async Task AnOptionFromAnotherContentItem_IsRejected(ShareContentType type)
    {
        var h = new Harness();
        var content = type == ShareContentType.Poll ? h.AddPoll() : h.AddCheck();
        var other = type == ShareContentType.Poll ? h.AddPoll() : h.AddCheck();

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(
            Request(content, other.Options[0].Id), VoterIdentity.ForUser(Guid.NewGuid())));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("optionId", exception.Field);
        Assert.Empty(h.Repository.Votes);
    }

    [Fact]
    public async Task ClosedExpiredAndInactiveContent_RejectVotes_AndSayWhy()
    {
        var h = new Harness();
        var closedPoll = h.AddPoll(open: false);
        var expiredPoll = h.AddPoll(expiresAt: DateTime.UtcNow.AddMinutes(-1));
        var inactiveCheck = h.AddCheck(open: false);
        var voter = VoterIdentity.ForUser(Guid.NewGuid());

        foreach (var content in new[] { closedPoll, expiredPoll, inactiveCheck })
        {
            var exception = await Assert.ThrowsAsync<AppException>(
                () => h.Voting.VoteAsync(Request(content, content.Options[0].Id), voter));
            Assert.Equal(400, exception.StatusCode);
        }

        Assert.Empty(h.Repository.Votes);
        Assert.Equal("CLOSED", (await h.Shared.GetAsync(closedPoll.ShareCode, VoterIdentity.ForUser(PollOwner))).Voting.BlockedReason);
        Assert.Equal("EXPIRED", (await h.Shared.GetAsync(expiredPoll.ShareCode, voter)).Voting.BlockedReason);
        Assert.Equal("INACTIVE", (await h.Shared.GetAsync(inactiveCheck.ShareCode, voter)).Voting.BlockedReason);
    }

    [Fact]
    public async Task UnknownShareCode_Returns404()
    {
        var h = new Harness();

        var get = await Assert.ThrowsAsync<AppException>(() => h.Shared.GetAsync("nope", null));
        var vote = await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = "nope", OptionId = Guid.NewGuid() }, VoterIdentity.ForUser(Guid.NewGuid())));

        Assert.Equal(404, get.StatusCode);
        Assert.Equal(404, vote.StatusCode);
    }

    [Fact]
    public async Task ADraftPoll_IsHiddenFromEveryoneButItsOwner()
    {
        var h = new Harness();
        var draft = h.AddPoll(open: false, readable: false);

        var anonymous = await Assert.ThrowsAsync<AppException>(() => h.Shared.GetAsync(draft.ShareCode, null));
        var voteAttempt = await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(
            Request(draft, draft.Options[0].Id), VoterIdentity.ForUser(Guid.NewGuid())));
        var owner = await h.Shared.GetAsync(draft.ShareCode, VoterIdentity.ForUser(PollOwner));

        Assert.Equal(404, anonymous.StatusCode);
        Assert.Equal(404, voteAttempt.StatusCode);
        Assert.True(owner.IsOwner);
    }

    [Fact]
    public async Task Results_StayHidden_UntilFiveOthersVoted_ForEveryone()
    {
        var h = new Harness();
        var locked = h.AddPoll();
        h.SeedVotes(locked, others: 3, ownerVote: true);
        var asOwner = await h.Shared.GetAsync(locked.ShareCode, VoterIdentity.ForUser(PollOwner));

        // A fourth other voter is still below the limit of five, even though they just voted themselves.
        var afterVote = await h.Voting.VoteAsync(Request(locked, locked.Options[0].Id), VoterIdentity.ForUser(Guid.NewGuid()));

        Assert.False(asOwner.Stats.ResultsVisible);
        Assert.Equal(3, asOwner.Stats.VoteProgress);
        Assert.All(asOwner.Options, option => Assert.Null(option.VoteCount));
        Assert.Null(asOwner.Stats.TotalVotes);
        Assert.False(afterVote.Stats.ResultsVisible);
        Assert.Equal(4, afterVote.Stats.VoteProgress);
        Assert.All(afterVote.Options, option => Assert.Null(option.Percentage));
    }

    [Theory]
    [InlineData(ShareContentType.Poll)]
    [InlineData(ShareContentType.Check)]
    public async Task Results_AreVisible_ToTheOwnerAndVoters_OnceUnlocked_ButNotToOthers(ShareContentType type)
    {
        var h = new Harness();
        var content = type == ShareContentType.Poll ? h.AddPoll() : h.AddCheck();
        var ownerId = content.OwnerId;
        h.SeedVotes(content, others: 5, ownerVote: false);
        var voterId = h.Repository.Votes.First().Voter;

        var owner = await h.Shared.GetAsync(content.ShareCode, VoterIdentity.ForUser(ownerId));
        var voter = await h.Shared.GetAsync(content.ShareCode, voterId);
        var stranger = await h.Shared.GetAsync(content.ShareCode, VoterIdentity.ForUser(Guid.NewGuid()));
        var anonymous = await h.Shared.GetAsync(content.ShareCode, null);

        Assert.True(owner.Stats.ResultsVisible);
        Assert.True(voter.Stats.ResultsVisible);
        Assert.False(stranger.Stats.ResultsVisible);
        Assert.False(anonymous.Stats.ResultsVisible);
        Assert.Equal(5, stranger.Stats.VoteProgress);
        Assert.All(stranger.Options, option => Assert.Null(option.VoteCount));
        Assert.Equal(5, owner.Stats.TotalVotes);
    }

    [Fact]
    public async Task Percentages_AreOfAllVotes_IncludingCustomAnswersAndTheOwner()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        // owner A, five friends: A, A, B, custom, custom => A 3, B 1, custom 2 of 6.
        h.AddVote(poll, VoterIdentity.ForUser(PollOwner), poll.Options[0].Id, null);
        h.AddVote(poll, VoterIdentity.ForGuest(Guid.NewGuid()), poll.Options[0].Id, "why");
        h.AddVote(poll, VoterIdentity.ForUser(Guid.NewGuid()), poll.Options[0].Id, null);
        h.AddVote(poll, VoterIdentity.ForUser(Guid.NewGuid()), poll.Options[1].Id, "why");
        h.AddVote(poll, VoterIdentity.ForUser(Guid.NewGuid()), null, null, "Rust");
        h.AddVote(poll, VoterIdentity.ForGuest(Guid.NewGuid()), null, null, "Go");

        var response = await h.Shared.GetAsync(poll.ShareCode, VoterIdentity.ForUser(PollOwner));

        Assert.True(response.Stats.ResultsVisible);
        Assert.Equal(6, response.Stats.TotalVotes);
        Assert.Equal(3, response.Options[0].VoteCount);
        Assert.Equal(50.00m, response.Options[0].Percentage);
        Assert.Equal(16.67m, response.Options[1].Percentage);
        Assert.Equal(2, response.Stats.CustomAnswerCount);
        Assert.Equal(33.33m, response.Stats.CustomAnswerPercentage);
        Assert.Equal(2, response.Stats.WhyCount);
        Assert.Equal(1, response.Options[1].WhyCount);
    }

    [Fact]
    public void Percentage_IsZero_WhenNobodyVoted()
    {
        Assert.Equal(0m, SharedContentMapper.Percentage(0, 0));
        Assert.Equal(33.33m, SharedContentMapper.Percentage(1, 3));
    }

    [Fact]
    public async Task TheVotersOwnChoice_IsShown_EvenWhileResultsAreHidden()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var voter = VoterIdentity.ForGuest(Guid.NewGuid());
        await h.Voting.VoteAsync(Request(poll, poll.Options[1].Id), voter);

        var response = await h.Shared.GetAsync(poll.ShareCode, voter);

        Assert.False(response.Stats.ResultsVisible);
        Assert.Equal(poll.Options[1].Id, response.Options.Single(option => option.IsSelected).Id);
    }

    [Fact]
    public async Task Anonymous_CanVoteIsTrue_ButHasNoVote()
    {
        var h = new Harness();
        var poll = h.AddPoll();

        var response = await h.Shared.GetAsync(poll.ShareCode, null);

        Assert.True(response.Voting.CanVote);
        Assert.False(response.Voting.HasVoted);
        Assert.Null(response.Voting.MyVote);
    }

    [Fact]
    public async Task InvalidRequests_AreRejected_BeforeAnythingIsStored()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var voter = VoterIdentity.ForUser(Guid.NewGuid());
        var option = poll.Options[0].Id;

        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(new VoteRequest { OptionId = option }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = "   ", OptionId = option }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = new string('a', 65), OptionId = option }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, OptionId = option, CustomAnswer = "x" }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, OptionId = option, Reason = "   " }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, CustomAnswer = new string('a', 301) }, voter));
        await Assert.ThrowsAsync<ValidationException>(() => h.Voting.VoteAsync(
            new VoteRequest { ShareCode = poll.ShareCode, OptionId = option, Reason = new string('a', 501) }, voter));

        Assert.Empty(h.Repository.Votes);
    }

    [Fact]
    public async Task Audit_RecordsTheOutcomeAndRole_WithoutTheAnswer()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var guest = VoterIdentity.ForGuest(Guid.NewGuid());

        await h.Voting.VoteAsync(Request(poll, poll.Options[0].Id, reason: "secret reason"), guest);
        await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(Request(poll, poll.Options[0].Id), guest));

        var success = Assert.Single(h.Audit.Polls, entry => entry.EventType == AuditEvents.Poll.VoteCast);
        var rejected = Assert.Single(h.Audit.Polls, entry => entry.EventType == AuditEvents.Poll.VoteRejected);
        Assert.Equal(AuditActorType.Guest, success.ActorType);
        Assert.Equal(poll.ContentId, success.PollId);
        Assert.Equal(ContentTypes.Poll, success.Metadata!["contentType"]);
        Assert.Equal(ApiErrorCodes.AlreadyVoted, rejected.ErrorCode);
        Assert.DoesNotContain(h.Audit.Polls, entry =>
            entry.Metadata is not null && entry.Metadata.Values.Any(value => value?.Contains("secret") == true));
    }

    [Fact]
    public async Task APollAndACheck_ShareOneResponseShape()
    {
        var h = new Harness();
        var poll = h.AddPoll();
        var check = h.AddCheck();

        var pollResponse = await h.Shared.GetAsync(poll.ShareCode, null);
        var checkResponse = await h.Shared.GetAsync(check.ShareCode, null);

        Assert.Equal(ContentTypes.Poll, pollResponse.ContentType);
        Assert.Equal(ContentTypes.Check, checkResponse.ContentType);
        Assert.Equal(poll.Options.Count, pollResponse.Options.Count);
        Assert.Equal(check.Options.Count, checkResponse.Options.Count);
        Assert.Equal(check.ShareCode, checkResponse.ShareCode);
    }

    [Fact]
    public async Task AVote_QueuesOnePushJobForTheOwner_WithoutAnswerContent()
    {
        var h = new Harness();
        var poll = h.AddPoll();

        await h.Voting.VoteAsync(Request(poll, poll.Options[0].Id, reason: "secret"), VoterIdentity.ForGuest(Guid.NewGuid()));

        var job = Assert.Single(h.PushQueue.Jobs);
        Assert.Equal(PollOwner, job.OwnerId);
        Assert.Equal(poll.ContentId, job.ContentId);
        Assert.True(job.IsPoll);
        Assert.Equal(poll.ShareCode, job.ShareCode);
    }

    [Fact]
    public async Task AnOwnerVotingOnTheirOwnPoll_QueuesNoPush()
    {
        var h = new Harness();
        var poll = h.AddPoll();

        await h.Voting.VoteAsync(Request(poll, poll.Options[0].Id), VoterIdentity.ForUser(PollOwner));

        Assert.Empty(h.PushQueue.Jobs);
    }

    [Fact]
    public async Task ARejectedVote_QueuesNoPush()
    {
        var h = new Harness();
        var poll = h.AddPoll(open: false);

        await Assert.ThrowsAsync<AppException>(() => h.Voting.VoteAsync(
            Request(poll, poll.Options[0].Id), VoterIdentity.ForUser(Guid.NewGuid())));

        Assert.Empty(h.PushQueue.Jobs);
    }

    [Fact]
    public async Task AQueueFailure_DoesNotFailTheVote()
    {
        var h = new Harness();
        h.PushQueue.Throw = true;
        var poll = h.AddPoll();

        var response = await h.Voting.VoteAsync(
            Request(poll, poll.Options[0].Id), VoterIdentity.ForUser(Guid.NewGuid()));

        Assert.NotNull(response);
    }

    private static VoteRequest Request(VotableContent content, Guid optionId, string? reason = null)
    {
        return new VoteRequest { ShareCode = content.ShareCode, OptionId = optionId, Reason = reason };
    }

    private sealed class Harness
    {
        public Harness()
        {
            Repository = new InMemoryVotableContents();
            Audit = new RecordingAuditLogWriter();
            var storage = new FakeObjectStorage();
            Shared = new SharedContentService(Repository, storage, MediaUploadPolicy.Default);
            Voting = new VotingService(
                Repository,
                storage,
                MediaUploadPolicy.Default,
                new VoteRequestValidator(),
                Audit,
                Streaks,
                PushQueue);
        }

        public RecordingPushQueue PushQueue { get; } = new();
        public NoOpStreakService Streaks { get; } = new();
        public InMemoryVotableContents Repository { get; }
        public RecordingAuditLogWriter Audit { get; }
        public SharedContentService Shared { get; }
        public VotingService Voting { get; }

        public VotableContent AddPoll(bool open = true, bool readable = true, DateTime? expiresAt = null)
        {
            var content = NewContent(ShareContentType.Poll, PollOwner, open, readable, expiresAt);
            Repository.Contents.Add(content);
            return content;
        }

        public VotableContent AddCheck(bool open = true)
        {
            var content = NewContent(ShareContentType.Check, CheckOwner, open, readable: true, expiresAt: null);
            Repository.Contents.Add(content);
            return content;
        }

        public void AddVote(
            VotableContent content,
            VoterIdentity voter,
            Guid? optionId,
            string? reason,
            string? custom = null)
        {
            Repository.Votes.Add(new StoredVote(content.ContentId, voter, new NewVote(optionId, custom, reason), DateTime.UtcNow));
        }

        /// <summary>Adds votes from <paramref name="others"/> distinct voters (and optionally the owner).</summary>
        public void SeedVotes(VotableContent content, int others, bool ownerVote)
        {
            if (ownerVote)
            {
                AddVote(content, VoterIdentity.ForUser(content.OwnerId), content.Options[0].Id, null);
            }

            for (var i = 0; i < others; i++)
            {
                var voter = i % 2 == 0 ? VoterIdentity.ForUser(Guid.NewGuid()) : VoterIdentity.ForGuest(Guid.NewGuid());
                AddVote(content, voter, content.Options[i % content.Options.Count].Id, i == 0 ? "why" : null);
            }
        }

        private static VotableContent NewContent(
            ShareContentType type,
            Guid owner,
            bool open,
            bool readable,
            DateTime? expiresAt)
        {
            var isPoll = type == ShareContentType.Poll;
            return new VotableContent
            {
                ContentType = type,
                ContentId = Guid.NewGuid(),
                ShareCode = Guid.NewGuid().ToString("N"),
                OwnerId = owner,
                Question = "Which one?",
                Status = open ? "ACTIVE" : (isPoll ? "CLOSED" : "INACTIVE"),
                IsOpen = open,
                PubliclyReadable = readable,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                Options =
                [
                    new VotableOption(Guid.NewGuid(), "A", 1),
                    new VotableOption(Guid.NewGuid(), "B", 2)
                ],
                Poll = isPoll ? new VotablePollDetails("QUESTION", null) : null,
                Check = isPoll ? null : new VotableCheckDetails(Guid.NewGuid(), "Trust", "#112233", "x")
            };
        }
    }

    private sealed record StoredVote(Guid ContentId, VoterIdentity Voter, NewVote Vote, DateTime At);

    private sealed class InMemoryVotableContents : IVotableContentRepository
    {
        public List<VotableContent> Contents { get; } = [];

        public List<StoredVote> Votes { get; } = [];

        public Task<VotableContent?> FindByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
            => Task.FromResult(Contents.FirstOrDefault(content => content.ShareCode == shareCode));

        public Task<PollVoteCountSnapshot> GetTallyAsync(VotableContent content, CancellationToken cancellationToken = default)
        {
            var rows = Votes
                .Where(stored => stored.ContentId == content.ContentId)
                .GroupBy(stored => stored.Vote.OptionId)
                .Select(group => new PollVoteCountRow(
                    content.ContentId,
                    group.Key,
                    group.Count(),
                    group.Count(stored => stored.Vote.Reason is not null),
                    group.Count(stored => stored.Voter.Id != content.OwnerId)));
            return Task.FromResult(
                PollVoteCountSnapshot.Combine(rows).GetValueOrDefault(content.ContentId) ?? PollVoteCountSnapshot.Empty);
        }

        public Task<MyVote?> GetVoteAsync(VotableContent content, VoterIdentity voter, CancellationToken cancellationToken = default)
        {
            var stored = Votes.FirstOrDefault(item => item.ContentId == content.ContentId && item.Voter.Id == voter.Id);
            return Task.FromResult(stored is null
                ? null
                : new MyVote(stored.Vote.OptionId, stored.Vote.CustomAnswer, stored.Vote.Reason, stored.At));
        }

        public async Task<VoteReceipt> AddVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            NewVote vote,
            Func<VoteReceipt, CancellationToken, Task>? afterAdded = null,
            CancellationToken cancellationToken = default)
        {
            if (Votes.Any(item => item.ContentId == content.ContentId && item.Voter.Id == voter.Id))
            {
                throw VoteExceptions.AlreadyVoted();
            }

            var now = DateTime.UtcNow;
            Votes.Add(new StoredVote(content.ContentId, voter, vote, now));
            var receipt = new VoteReceipt(Guid.NewGuid(), now);
            if (afterAdded is not null)
            {
                await afterAdded(receipt, cancellationToken);
            }

            return receipt;
        }
    }
}
