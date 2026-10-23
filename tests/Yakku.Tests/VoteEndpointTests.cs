using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yakku.API.Controllers;
using Yakku.API.Media;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.Services;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Votes.DTOs;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Wiring of the unified share and vote endpoints: caller resolution and what reaches the services.</summary>
public class VoteEndpointTests
{
    private const string GuestToken = "guest-token-for-endpoint-tests";

    [Fact]
    public async Task Vote_AsAGuest_PassesAGuestIdentity_AndReturns201()
    {
        var h = new Harness();
        h.Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;

        var result = await h.Votes.Vote(new VoteRequest { ShareCode = "abc", OptionId = Guid.NewGuid() }, CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(VoterIdentity.ForGuest(h.Guest.Id), h.Voting.LastVoter);
        Assert.Null(h.Voting.LastExpectedType);
        Assert.IsType<ApiResponse<SharedContentResponse>>(created.Value);
    }

    [Fact]
    public async Task Vote_AsASignedInUser_PrefersTheUserOverAGuestToken()
    {
        var h = new Harness();
        var userId = Guid.NewGuid();
        h.SignIn(userId);
        h.Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;

        await h.Votes.Vote(new VoteRequest { ShareCode = "abc", OptionId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(VoterIdentity.ForUser(userId), h.Voting.LastVoter);
    }

    [Fact]
    public async Task Vote_WithoutAnyIdentity_Throws401_AndNeverReachesTheService()
    {
        var h = new Harness();

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Votes.Vote(
            new VoteRequest { ShareCode = "abc", OptionId = Guid.NewGuid() }, CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(0, h.Voting.Calls);
    }

    [Fact]
    public async Task Vote_WithAnExpiredGuestToken_Throws401()
    {
        var h = new Harness(guestExpired: true);
        h.Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;

        var exception = await Assert.ThrowsAsync<AppException>(() => h.Votes.Vote(
            new VoteRequest { ShareCode = "abc", OptionId = Guid.NewGuid() }, CancellationToken.None));

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal(0, h.Voting.Calls);
    }

    [Fact]
    public async Task Share_Get_TreatsAnInvalidGuestTokenAsAnonymous_InsteadOfFailing()
    {
        var h = new Harness();
        h.Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = "not-a-real-token";

        var result = await h.Shares.Get("abc", CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result).StatusCode);
        Assert.Null(h.Shared.LastViewer);
        Assert.Equal("abc", h.Shared.LastCode);
    }

    [Fact]
    public async Task Share_Get_PassesTheGuestAsTheViewer()
    {
        var h = new Harness();
        h.Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;

        await h.Shares.Get("abc", CancellationToken.None);

        Assert.Equal(VoterIdentity.ForGuest(h.Guest.Id), h.Shared.LastViewer);
    }

    private sealed class Harness
    {
        public Harness(bool guestExpired = false)
        {
            var hasher = new GuestTokenHasher();
            Guest = new Guest(
                hasher.Hash(GuestToken),
                guestExpired ? DateTime.UtcNow.AddDays(-30) : DateTime.UtcNow);
            var guests = new InMemoryGuestRepository();
            guests.Seed(Guest);

            Http = new DefaultHttpContext();
            var resolver = new MediaCallerResolver(guests, hasher);
            Voting = new StubVotingService();
            Shared = new StubSharedContentService();

            var context = new ControllerContext { HttpContext = Http };
            Votes = new VotesController(Voting, resolver) { ControllerContext = context };
            Shares = new SharesController(Shared, resolver) { ControllerContext = context };
        }

        public Guest Guest { get; }
        public DefaultHttpContext Http { get; }
        public StubVotingService Voting { get; }
        public StubSharedContentService Shared { get; }
        public VotesController Votes { get; }
        public SharesController Shares { get; }

        public void SignIn(Guid userId)
        {
            Http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "test"));
        }
    }

    private sealed class StubVotingService : IVotingService
    {
        public int Calls { get; private set; }
        public VoteRequest? LastRequest { get; private set; }
        public VoterIdentity? LastVoter { get; private set; }
        public ShareContentType? LastExpectedType { get; private set; }

        public VoteOutcome Outcome { get; private set; } = new(
            new SharedContentResponse { ContentId = Guid.NewGuid() },
            new VoteReceipt(Guid.NewGuid(), new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc)));

        public Task<SharedContentResponse> VoteAsync(
            VoteRequest request,
            VoterIdentity voter,
            CancellationToken cancellationToken = default)
        {
            return CastAsync(request, voter, null, cancellationToken).ContinueWith(task => task.Result.Content, cancellationToken);
        }

        public Task<VoteOutcome> CastAsync(
            VoteRequest request,
            VoterIdentity voter,
            ShareContentType? expectedType = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastRequest = request;
            LastVoter = voter;
            LastExpectedType = expectedType;

            // Echo the caller's choice back the way the real service does.
            Outcome = Outcome with
            {
                Content = new SharedContentResponse
                {
                    ContentId = Outcome.Content.ContentId,
                    Voting = new VotingStateResponse
                    {
                        HasVoted = true,
                        MyVote = new MyVoteResponse
                        {
                            OptionId = request.OptionId,
                            CustomAnswer = request.CustomAnswer,
                            Reason = request.Reason,
                            VotedAt = Outcome.Receipt.CreatedAt
                        }
                    }
                }
            };
            return Task.FromResult(Outcome);
        }
    }

    private sealed class StubSharedContentService : ISharedContentService
    {
        public string? LastCode { get; private set; }
        public VoterIdentity? LastViewer { get; private set; }

        public Task<SharedContentResponse> GetAsync(
            string shareCode,
            VoterIdentity? viewer,
            CancellationToken cancellationToken = default)
        {
            LastCode = shareCode;
            LastViewer = viewer;
            return Task.FromResult(new SharedContentResponse { ShareCode = shareCode });
        }
    }
}
